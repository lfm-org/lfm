# Threat Model: Cosmos Partition Key Authorization

## Introduction

This document covers the trust boundary between authenticated API function handlers
and Azure Cosmos DB, where the application enforces user-level isolation by using
`battleNetId` as both the document identifier and the partition key for the `raiders`
container. An attacker who has obtained a valid session can attempt to read or modify
another user's raider document by supplying a crafted `battleNetId`, exploiting a
missing partition-key enforcement in a repository method, or leveraging cross-partition
queries that return documents outside the caller's scope.

## Data Flow

```
Auth'd Browser         Function Handler          Repository Layer           Cosmos DB
      |                       |                         |                       |
      |--GET /api/me--------->|                         |                       |
      |                       |--TryGetPrincipal()      |                       |
      |                       |  => {BattleNetId}       |                       |
      |                       |--GetByBattleNetIdAsync->|                       |
      |                       |  [boundary: handler -> Cosmos]
      |                       |                         |--ReadItemAsync(id,    |
      |                       |                         |   PartitionKey(id))-->|
      |                       |                         |<--RaiderDocument------|
      |<--200 {raider data}---|                         |                       |
      |                       |                         |                       |
      |--GET /api/runs------->|                         |                       |
      |                       |--ListForGuildAsync()---->                       |
      |                       |  (principal.BattleNetId + principal.GuildId)    |
      |                       |                         |--cross-partition----->|
      |                       |                         |  query with @params   |
      |<--200 [{runs}]--------|                         |                       |
```

## Trust Boundaries Crossed

- **Authenticated request to repository layer**: function handlers receive a
  `SessionPrincipal` from middleware; they must pass `principal.BattleNetId` (not a
  caller-supplied value) as the partition key to repository methods.
- **Repository layer to Cosmos data plane**: the SDK call specifies the partition key;
  Cosmos enforces partition isolation at the storage layer. The application layer
  must also enforce it — Cosmos does not know which user "should" own a document.
- **Managed identity to Cosmos**: the Functions app uses `DefaultAzureCredential` with
  the Cosmos Built-in Data Contributor role; there are no per-user credentials at the
  Cosmos level.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker sends `?battleNetId=victim` in a query string and a repository method uses that value as the partition key instead of the session principal. | `RaidersRepository.GetByBattleNetIdAsync`, `UpsertAsync`, and `ReplaceAsync` are only called from handlers that supply `principal.BattleNetId` from the verified session, not from request parameters. The `idempotency` container follows the same pattern: `IdempotencyMiddleware` derives the `battleNetId` from `ctx.GetPrincipal()`. | Medium — not enforced at the repository API level; a future handler calling any of these repositories with a request-supplied id would introduce BOLA silently. |
| **Tampering** | Attacker submits an `ETag`-free PUT/PATCH to overwrite another user's run, raider, or guild document. | Every `ReplaceAsync` overload — `RaidersRepository`, `RunsRepository`, `GuildRepository` — passes an `IfMatchEtag` to Cosmos and translates 412 to a `ConcurrencyConflictException`. Handlers honour `If-Match` on `PUT /runs/{id}`, `PATCH /me`, `PATCH /guild`. | Low — write-fencing is now repo-level for the user-write paths. The outstanding gap is **ownership** (is the caller the document's owner), not **fencing** (is the etag current). |
| **Tampering — admin migration** | `RunsMigrateSchemaFunction` runs `SELECT * FROM c` over the runs container and unconditionally `ReplaceItemAsync`s every result without `IfMatchEtag`. A bug in `RunModeResolver` or in the admin gate could overwrite arbitrary live runs. | Access gated by `ISiteAdminService.IsAdminAsync`. Intended as a one-shot post-deploy operation. | Medium — admin-only and single-shot, but no optimistic concurrency on the migration writes. Detailed coverage in `admin-privilege-boundary.md`. |
| **Repudiation** | Attacker denies having modified a document; write audit trail is absent. | Cosmos diagnostic settings (`DataPlaneRequests`, `QueryRuntimeStatistics`) forwarded to Log Analytics. `CosmosRequestChargeLogger` emits structured per-op logs (operation, container, partition key, RU charge) on every call. `AuditMiddleware` logs HTTP function start/end; per-handler `AuditLog.Emit` records mutating actions with the actor id HMAC-hashed via `IActorHasher`. | Medium — Cosmos diagnostic logs capture operation type and resource but not full document diff. Actor ids in audit logs are HMAC-pseudonymized when `AuditOptions.HashSalt` is set; **today the salt is not wired in `infra/modules/functions.bicep`, so actor ids surface as plaintext until that gap is closed.** |
| **Information disclosure** | Cross-partition query in `ListExpiredAsync` returns all raiders; attacker with Functions MI compromise could read all user profiles. | `disableLocalAuth: true` on the Cosmos account prevents key-based access. Only the Functions MI with Built-in Data Contributor role can query. Network access is restricted to Azure datacenter IPs (`ipRules: [0.0.0.0]`). The cleanup query projects only `id` and `battleNetId`. | Medium — the `0.0.0.0` IP rule permits all Azure-datacenter traffic, not just the single Functions app. Any other Azure resource in the same region could reach the Cosmos endpoint. |
| **Information disclosure — admin / scrub queries** | `RunsRepository.ScrubRaiderAsync` (account-deletion cascade) and `RunsRepository.MigrateSchemaAsync` (admin one-shot) execute the broadest cross-partition reads in the codebase. `MigrateSchemaAsync` returns full `RunDocument` payloads with no projection. | Both paths are gated: scrub runs only from `MeDeleteFunction` for the caller's own `battleNetId`; migrate runs only from `RunsMigrateSchemaFunction` after `ISiteAdminService` admit. | Medium — admin compromise plus `MigrateSchemaAsync` reveals every run's full content. See `admin-privilege-boundary.md`. |
| **Denial of service** | Attacker triggers many cross-partition queries to exhaust the 1,000 RU/s free-tier throughput. | Runs container has a composite index optimising the `visibility + creatorGuild + startTime` query. Raiders container only exposes the cleanup query as cross-partition. `CosmosRequestChargeLogger` makes runaway-cost queries observable post-hoc. | Medium — no per-user RU cap; a single authenticated user could trigger many concurrent cross-partition queries and impact all users. |
| **Elevation of privilege** | Attacker exploits a BOLA flaw where `GetByIdAsync` (runs container) returns a document without verifying `creatorBattleNetId` matches the session. | `RunsRepository.GetByIdAsync` is a pure point-read by `id`; handler code must validate ownership before exposing the document. `IGuildPermissions` provides a centralised authorisation helper for guild-scoped operations (signup, run create, run delete by guild rank). | Medium — `IGuildPermissions` covers guild-scope authorisation; **run-creator ownership** (is the caller the run's `creatorBattleNetId`?) is still scattered across handlers with no central helper. Account-deletion cascade through `MeDeleteFunction` is its own write-flow within the partition boundary. |

## Key Code References

- `api/Program.cs:110-145` — Cosmos client construction (singleton): uses
  `DefaultAzureCredential` when `AuthKey` is absent (production path), falling back
  to key for local dev / emulator. SDK retry policy capped at 9 attempts / 30 s for
  rate-limited requests.
- `api/Program.cs:171-173` — repository registrations (`IRaidersRepository`,
  `IRunsRepository`, `IGuildRepository`).
- `api/Program.cs:176-177` — `IIdempotencyStore` and `IGuildPermissions` registrations.
- `api/Repositories/RaidersRepository.cs:16-31` — `GetByBattleNetIdAsync` point-read
  uses caller-supplied `battleNetId` as both id and `PartitionKey`; isolation holds
  only if the caller provides `principal.BattleNetId`. Logs RU charge.
- `api/Repositories/RaidersRepository.cs:33-40` — `UpsertAsync` partitioned on
  `raider.BattleNetId`.
- `api/Repositories/RaidersRepository.cs:42-60` — `ReplaceAsync` enforces
  `IfMatchEtag`, throws `ConcurrencyConflictException` on 412.
- `api/Repositories/RaidersRepository.cs:62-76` — `DeleteAsync`.
- `api/Repositories/RaidersRepository.cs:78-101` — `ListExpiredAsync` cross-partition
  query, projects only `id` and `battleNetId`.
- `api/Repositories/RunsRepository.cs:17-41` — `ListForGuildAsync` cross-partition
  query, filters by `@battleNetId` and `@guildId` from the session principal.
- `api/Repositories/RunsRepository.cs:112-133` — `UpdateAsync(run, ifMatchEtag, ct)` —
  caller may pass an explicit `If-Match`, otherwise falls back to `run.ETag`. Throws
  `ConcurrencyConflictException` on 412.
- `api/Repositories/RunsRepository.cs:151-188` — `ScrubRaiderAsync` cross-partition
  account-deletion cascade.
- `api/Repositories/RunsRepository.cs:191-224` — `MigrateSchemaAsync` admin one-shot:
  `SELECT * FROM c` over the entire `runs` container, full-document writes without
  ETag.
- `api/Repositories/CosmosRequestChargeLogger.cs` — structured RU/op logger invoked
  by every repository.
- `api/Repositories/ConcurrencyConflictException.cs` — translation of Cosmos 412 to
  a domain exception consumed by handlers as `Problem.Conflict(...)`.
- `api/Repositories/GuildRepository.cs:16-31` — `GetAsync` point-read by `guildId`
  partition key; guilds are not user-private resources, so no per-user check applies.
- `infra/modules/cosmos.bicep:31-32` — `disableLocalAuth: true` and
  `disableKeyBasedMetadataWriteAccess: true` enforce managed-identity-only access.
- `infra/modules/cosmos.bicep:33-35` — `ipRules: [0.0.0.0]` (Azure-datacenter-only).
- `infra/modules/cosmos.bicep:51-58` — `raiders` container, partition key
  `/battleNetId` (the user-isolation boundary).
- `infra/modules/cosmos.bicep:80-100` — `runs` container, partition key `/id` (run id,
  not user id); user isolation on runs is query-level, not partition-level. Composite
  index for the guild-list query.
- `infra/modules/cosmos.bicep:130-146` — `idempotency` container, partition key
  `/battleNetId` (same isolation pattern as raiders; covered by the Spoofing row).
- `infra/modules/functions.bicep:154-168` — Cosmos Built-in Data Contributor role
  granted to Functions MI; scope is the account, not a single container.

## Out of Scope

- Network-level Cosmos access controls beyond the `ipRules` already documented.
- Cosmos account-level management-plane RBAC (control plane) — this covers data plane
  only.
- The session cookie that supplies `BattleNetId` to the handler — covered by
  `session-cookie-api.md`.
- Admin-driven mass mutation paths (`RunsMigrateSchemaFunction`, the admin scrub flow)
  — full coverage in `admin-privilege-boundary.md`; this model only flags the data-plane
  surface.
- Idempotency replay-cache semantics, TTL, and key-collision concerns — covered by
  `idempotency-replay-cache.md` (backlog).
- Static reference-data containers (`ExpansionsRepository`, `InstancesRepository`,
  `SpecializationsRepository`) — these are blob-backed, not Cosmos-backed; covered by
  `reference-data-integrity.md` (backlog).
