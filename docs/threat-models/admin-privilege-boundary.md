# Threat Model: Admin Privilege Boundary

## Introduction

This document covers the trust boundary between an authenticated regular user and the
site-admin endpoints that execute privileged, application-wide operations:
`POST /api/admin/runs/migrate-schema`, `POST /api/wow/reference/refresh`, and
`GET /api/guild/admin`. Admin identity is determined by membership in the
`site-admin-battle-net-ids` Key Vault secret, evaluated by `SiteAdminService` with a
10-second in-memory cache. An attacker who acquires admin status — through compromise,
mis-grant, or a window in the cache TTL — can mutate every run document, overwrite
every reference-data blob the SPA consumes, or read every guild's full configuration.

## Data Flow

```
Auth'd Browser           Function Handler              SiteAdminService           Key Vault
      |                         |                            |                       |
      |--POST /api/admin/runs/migrate-schema (cookie)-->     |                       |
      |                  [boundary: user trust -> admin trust]
      |                         |--principal=GetPrincipal()  |                       |
      |                         |--IsAdminAsync(principal)-->|                       |
      |                         |                            |--cache hit? (<=10s)   |
      |                         |                            |  yes -> return       |
      |                         |                            |  no  -> SecretClient->|
      |                         |                            |<--ids list-----------|
      |                         |<--true / false-------------|                       |
      |                         |  if false -> 403           |                       |
      |                         |--RunsRepository.MigrateSchemaAsync()->             |
      |                         |  (SELECT * FROM c, full document overwrite,        |
      |                         |   no IfMatchEtag, every partition)                 |
      |<--200 / NDJSON progress-|                                                    |
```

## Trust Boundaries Crossed

- **Authenticated user → admin trust zone**: every admin endpoint resolves the
  session principal first (via the standard `[RequireAuth]` chain), then calls
  `ISiteAdminService.IsAdminAsync(principal.BattleNetId, ct)`; only on a true result
  does the handler execute the privileged operation.
- **Functions MI → Key Vault for the admin allowlist**: `KeyVaultSecretResolver`
  reads `site-admin-battle-net-ids`. The same `Key Vault Secrets User` role that
  unwraps the Data Protection key reads this secret (see
  `keyvault-data-protection-wrap.md`).
- **Admin handler → Cosmos / Blob**: privileged writes that bypass per-user
  partition isolation. `RunsMigrateSchemaFunction` writes every document in the
  `runs` container; `WowReferenceRefreshFunction` writes every blob under
  `wow/reference/`.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker exploits the 10-second cache TTL: after the operator removes a Battle.net id from the secret, the attacker still gets one admin-decision window per Functions instance. | `CacheTtl = TimeSpan.FromSeconds(10)`; cache is per-instance, so revocation propagates within ~10 s on each warm instance. Operator can recycle the Functions app to invalidate every cache atomically. | Medium — silent revocation lag of up to 10 s plus existing in-flight requests. No way to force-flush from outside Azure. |
| **Tampering — schema migration** | `RunsRepository.MigrateSchemaAsync` runs a full `SELECT * FROM c` over the runs container and `ReplaceItemAsync`s every result without `IfMatchEtag`. A bug in `RunModeResolver` or in any future migration logic could overwrite arbitrary live runs racing with concurrent user PUT/PATCH operations. | Access gated by `IsAdminAsync`. Intended as a one-shot post-deploy operation; not part of any business flow. | Medium — admin-only and expected to be one-shot, but no optimistic concurrency on the migration writes. A retry loop or a buggy migration silently clobbers user edits in flight. Document the operation as "drain user traffic before invoking" in the runbook. |
| **Tampering — reference data** | `WowReferenceRefreshFunction` writes every `wow/reference/{kind}/{slug}` blob and the matching `index.json`. A compromised admin or a flaw in `ReferenceSync` could replace `index.json` with a manifest that points to attacker-controlled instance/spec data. The SPA consumes this for instance and specialization pickers. | Admin-gated. Blob writes go through `BlobReferenceClient` over RBAC (`allowSharedKeyAccess: false`). The blob source data comes from the Blizzard Game Data API via `BlizzardGameDataClient`. | Medium — see `reference-data-integrity.md` (backlog) for the full read-side blast radius. There is no integrity hash or signature verification on `index.json`. |
| **Repudiation** | Admin denies having triggered a destructive admin action. | `AuditMiddleware` records function entry / exit. Per-handler `AuditLog.Emit` writes admin actions with the actor id (HMAC-hashed via `IActorHasher` when `AuditOptions.HashSalt` is set). `Audit__HashSalt` is wired in Bicep from the `audit-hash-salt` Key Vault secret (`docs/security-architecture.md`). KV `categoryGroup: audit` records the secret read on every cache refill. | Medium — no document-diff audit on `MigrateSchemaAsync`; logs say "admin X invoked migrate" but not "admin X overwrote document Y from value A to B." App Insights ingestion is best-effort. If the operator has not populated `audit-hash-salt`, runtime falls back to plaintext (fail-open); see `audit-log-pii-pipeline.md` (backlog) for the full pipeline view. |
| **Information disclosure — guild admin** | `GuildAdminFunction` returns any guild document by caller-supplied `guildId` query parameter. An admin compromise leaks every guild's roster, mode preferences, and creator id. | Admin-gated. No further authorization within the handler — admin is trusted to read any guild. | Medium — single-factor admin compromise = full read of all guild configurations. Acceptable for the current trust model (admin role is intentionally privileged) but worth documenting as the blast radius of admin compromise. |
| **Denial of service — refresh storm** | An admin (or a successful CSRF / replay against an admin session) repeatedly calls `POST /api/wow/reference/refresh`; each call drives a full sweep of the Blizzard Game Data API plus ~500 blob writes. There is no idempotency key or de-duplication on this endpoint. | Admin-gated. `BlizzardRateLimiter` throttles outbound calls; `RateLimitMiddleware` applies origin rate limits to the inbound endpoint. | Medium — exhausts Blizzard's per-app rate window and the storage write quota; recovery requires waiting for both. Acceptable mitigation in a hobby-cost stance; would warrant idempotency-key + 202 Accepted for any production use. |
| **Elevation of privilege** | A non-admin acquires admin status via cache mis-population (e.g., the admin secret returns extra ids after a malformed update). | `KeyVaultSecretResolver` reads the secret as a single string; `SiteAdminService` parses by line and trims; ids list seeded from the cleartext secret only. Fail-open-to-empty on KV error (no admin granted on read failure). | Low — failure modes default to "no admin," not "all admin." A malformed write to the secret is operator error; KV soft-delete preserves the previous version for recovery. |

## Key Code References

- `api/Services/SiteAdminService.cs:15` — "When KeyVaultUrl is not configured,
  IsAdminAsync always returns false."
- `api/Services/SiteAdminService.cs:24` — `CacheTtl = TimeSpan.FromSeconds(10)`.
- `api/Services/SiteAdminService.cs:32-67` — `IsAdminAsync` flow: TTL check, KV read,
  fail-open-to-empty on error.
- `api/Services/KeyVaultSecretResolver.cs` — singleton `SecretClient` constructed
  with `DefaultAzureCredential`; reads `site-admin-battle-net-ids`.
- `api/Functions/RunsMigrateSchemaFunction.cs:44` — admin gate.
- `api/Functions/RunsMigrateSchemaFunction.cs:38, 71` — primary + `/api/v1/` route
  registrations.
- `api/Repositories/RunsRepository.cs:191-224` — `MigrateSchemaAsync` —
  `SELECT * FROM c`, full-document `ReplaceItemAsync` per result, no `IfMatchEtag`.
- `api/Functions/WowReferenceRefreshFunction.cs:65` — admin gate.
- `api/Functions/WowReferenceRefreshFunction.cs:38` — comment confirming the
  repo-wide pattern: `AuthorizationLevel.Anonymous` at the host trigger,
  `[RequireAuth]` at the policy layer.
- `api/Functions/GuildAdminFunction.cs:32` — admin gate.
- `api/Services/ReferenceSync.cs` — orchestrates Blizzard pulls + blob writes;
  consumed only by `WowReferenceRefreshFunction` and the timer.

## Out of Scope

- The Battle.net OAuth callback that issues the session a user later promotes via
  admin allowlist — covered by `battlenet-oauth-callback.md`.
- The Cosmos partition-key boundary admin operations bypass — `cosmos-partition-key-authz.md`
  flags the schema migration's data-plane footprint; this model owns the privilege
  decision.
- The Key Vault role-grant blast radius (one role covers DP key + admin allowlist
  + Blizzard credentials) — covered by `keyvault-data-protection-wrap.md`.
- The reference-data **read** path (every authenticated user fetches `index.json`)
  — covered by `reference-data-integrity.md` (backlog).
- The Blizzard outbound API surface that admin refresh exercises — covered by
  `blizzard-outbound-api.md`.
- The audit-log pipeline (HMAC actor hashing, salt rotation, App Insights ingestion)
  — covered by `audit-log-pii-pipeline.md` (backlog).
