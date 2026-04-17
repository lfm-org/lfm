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
| **Spoofing** | Attacker sends `?battleNetId=victim` in a query string and a repository method uses that value as the partition key instead of the session principal. | `RaidersRepository.GetByBattleNetIdAsync` and `UpsertAsync` are only called from handlers that supply `principal.BattleNetId` from the verified session, not from request parameters. | Medium — not enforced at the repository API level; a future handler calling the repository with a request-supplied ID would introduce BOLA silently. |
| **Tampering** | Attacker submits an `ETag`-free PUT to overwrite another user's run document. | `RunsRepository.UpdateAsync` passes `IfMatchEtag = run.ETag` to Cosmos, providing optimistic concurrency. `DeleteAsync` operates by `run.Id` + `PartitionKey(run.Id)` and should only be called after ownership is verified by the handler. | Medium — ownership check is in function handlers, not in repository methods; no enforce-at-repo pattern. |
| **Repudiation** | Attacker denies having modified a document; write audit trail is absent. | Cosmos diagnostic settings (`DataPlaneRequests`, `QueryRuntimeStatistics`) are forwarded to Log Analytics. `AuditMiddleware` logs requests at the HTTP layer. | Medium — Cosmos diagnostic logs capture operation type and resource but not full document diff. No application-level write audit. |
| **Information disclosure** | Cross-partition query in `ListExpiredAsync` returns all raiders; attacker with Functions MI compromise could read all user profiles. | `disableLocalAuth: true` in `cosmos.bicep` prevents key-based access. Only the Functions MI with Built-in Data Contributor role can query. Network access is restricted to Azure datacenter IPs (`ipRules: [{0.0.0.0}]`). | Medium — the `{0.0.0.0}` IP rule permits all Azure-datacenter traffic, not just the single Functions app. Any other Azure resource in the same region could reach the Cosmos endpoint. |
| **Denial of service** | Attacker triggers many cross-partition queries to exhaust the 1,000 RU/s free-tier throughput. | Runs container has a composite index optimizing the `visibility + creatorGuild + startTime` query. Raiders container only exposes the cleanup query as cross-partition. | Medium — no per-user RU cap; a single authenticated user could trigger many concurrent cross-partition queries and impact all users. |
| **Elevation of privilege** | Attacker exploits a BOLA flaw where `GetByIdAsync` (runs container) returns a document without verifying `creatorBattleNetId` matches the session. | `RunsRepository.GetByIdAsync` is a pure point-read by `id`; handler code must validate ownership before exposing the document. The repository has no knowledge of the calling user. | Medium — authorization is handler-responsibility, not repository-enforced. No centralized ownership-check helper currently exists. |

## Key Code References

- `api/Program.cs:98-100` — Cosmos client construction: uses `DefaultAzureCredential`
  when `AuthKey` is absent (production path), falling back to key for local dev.
- `api/Repositories/RaidersRepository.cs:16-20` — point-read uses caller-supplied
  `battleNetId` as both the document ID and the `PartitionKey`; isolation holds only
  if the caller provides `principal.BattleNetId`.
- `api/Repositories/RaidersRepository.cs:29-33` — `UpsertAsync` uses
  `raider.BattleNetId` as partition key; the document's own field governs placement.
- `api/Repositories/RaidersRepository.cs:51-71` — `ListExpiredAsync` is a
  cross-partition query; it projects only `id` and `battleNetId` to limit data exposure.
- `api/Repositories/RunsRepository.cs:12-33` — `ListForGuildAsync` cross-partition
  query filters by `@battleNetId` and `@guildId`; both come from the session principal
  in the calling handler.
- `api/Repositories/RunsRepository.cs:95-112` — `UpdateAsync` enforces optimistic
  concurrency via `IfMatchEtag`; no explicit owner check at the repository level.
- `api/Repositories/GuildRepository.cs:12-25` — `GetAsync` point-read by `guildId`
  partition key; guilds are not user-private resources, so no per-user check applies.
- `infra/modules/cosmos.bicep:28-29` — `disableLocalAuth: true` and
  `disableKeyBasedMetadataWriteAccess: true` enforce managed-identity-only access.
- `infra/modules/cosmos.bicep:48-55` — `raiders` container partition key `/battleNetId`
  defines the isolation boundary.
- `infra/modules/cosmos.bicep:77-84` — `runs` container partition key `/id` (run ID,
  not user ID); user isolation on runs is query-level, not partition-level.
- `infra/modules/functions.bicep:148-157` — Cosmos Built-in Data Contributor role
  granted to Functions MI; scope is the account, not a single container.

## Out of Scope

- Network-level Cosmos access controls beyond the `ipRules` already documented.
- Cosmos account-level management-plane RBAC (control plane) — this covers data plane
  only.
- The session cookie that supplies `BattleNetId` to the handler — covered by
  `session-cookie-api.md`.
