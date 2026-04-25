# Threat Models

STRIDE threat models for LFM's distinct trust boundaries. Each file is one
boundary, one trust transition. Documents are claude-facing first (intended to
inform code review and secure design choices) but kept readable for any reviewer.

## Why this directory exists

Per the rubric in
`souroldgeezer-audit/docs/security-reference/devsecops.md` § 7 principle 10,
**threat modelling is part of the diff**. A design change that introduces a new
trust boundary without a threat model is incomplete. Per § 3, "what to threat
model" includes:

- Every new external interface (HTTP, queue, webhook, RPC, file upload, WebSocket).
- Every new trust boundary (process, network, tenant, service account, build host).
- Every elevation-of-privilege path (admin endpoints, impersonation, sudo, role
  assumption).
- Every data flow that crosses a classification boundary (PII, secrets, regulated
  data).
- Every design change to authentication, authorization, session management,
  cryptography, or audit logging.

If a PR matches one of those, it must ship a threat-model diff in the same PR —
either updating an existing file under this directory or adding a new one. The
positive signal in § 5.3 #12 is exactly this: "threat model updated in the same PR
as the architectural change it covers."

## Current models (mapped to trust boundaries)

| File | Boundary |
|---|---|
| [`battlenet-oauth-callback.md`](battlenet-oauth-callback.md) | Unauthenticated browser → `/api/battlenet/callback` (PKCE auth-code completion + session cookie issuance). |
| [`session-cookie-api.md`](session-cookie-api.md) | Browser holding `lfm_session` → `[RequireAuth]` API endpoints. |
| [`cosmos-partition-key-authz.md`](cosmos-partition-key-authz.md) | Authenticated handlers → Cosmos data plane; `/battleNetId` partition isolation. |
| [`keyvault-data-protection-wrap.md`](keyvault-data-protection-wrap.md) | Functions app → Key Vault for the DP-wrap key, admin-allowlist secret, Blizzard credentials. |
| [`admin-privilege-boundary.md`](admin-privilege-boundary.md) | Authenticated regular user → site-admin endpoints (`/admin/runs/migrate-schema`, `/wow/reference/refresh`, `/guild/admin`). |
| [`blizzard-outbound-api.md`](blizzard-outbound-api.md) | Functions app → Blizzard OAuth + Profile + Game Data APIs. |
| [`run-signup-peer-permission.md`](run-signup-peer-permission.md) | Authenticated user A → run document owned by user B (signup roster mutations). |
| [`e2e-login-bypass.md`](e2e-login-bypass.md) | E2E test runner → arbitrary session cookie via the compile-and-runtime-gated `/api/e2e/login` endpoint. |

## Coverage map for `api/Functions/*.cs`

| Function | Covered by |
|---|---|
| `BattleNetLoginFunction`, `BattleNetCallbackFunction` | `battlenet-oauth-callback.md` |
| `BattleNetLogoutFunction` | `session-cookie-api.md` (cookie clearing path) |
| `BattleNetCharactersFunction`, `BattleNetCharactersRefreshFunction`, `BattleNetCharacterPortraitsFunction` | `blizzard-outbound-api.md` (outbound), `session-cookie-api.md` (cookie supplies user token); portrait detail in `character-portrait-fetch.md` (backlog) |
| `MeFunction`, `MeUpdateFunction`, `MeDeleteFunction` | `session-cookie-api.md` + `cosmos-partition-key-authz.md` |
| `GuildFunction` (GET / PATCH) | `cosmos-partition-key-authz.md` (partition + IGuildPermissions) |
| `GuildAdminFunction` | `admin-privilege-boundary.md` |
| `RaiderCharacterAddFunction`, `RaiderCharacterEnrichFunction`, `RaiderCharacterFunction`, `RaiderCleanupFunction` | `blizzard-outbound-api.md` + `cosmos-partition-key-authz.md` |
| `RunsListFunction`, `RunsDetailFunction` | `cosmos-partition-key-authz.md` |
| `RunsCreateFunction`, `RunsUpdateFunction`, `RunsDeleteFunction` | `cosmos-partition-key-authz.md` (data plane); run-creator ownership gap noted in that file's Elevation row |
| `RunsSignupFunction`, `RunsCancelSignupFunction` | `run-signup-peer-permission.md` |
| `RunsMigrateSchemaFunction` | `admin-privilege-boundary.md` |
| `WowReferenceRefreshFunction`, `WowReferenceRefreshTimerFunction` | `admin-privilege-boundary.md` (write side); read side in `reference-data-integrity.md` (backlog) |
| `WowReferenceExpansionsFunction`, `WowReferenceInstancesFunction`, `WowReferenceSpecializationsFunction` | `reference-data-integrity.md` (backlog) |
| `PrivacyContactFunction` | not yet modelled — see backlog |
| `HealthFunction` | not modelled — public diagnostic, no state |
| `CorsPreflightFunction` | `session-cookie-api.md` (origin allowlist) |
| `E2ELoginFunction` | `e2e-login-bypass.md` |

## Backlog (medium-priority gaps; add when touched)

These represent boundaries where the existing models flag the surface in their
"Out of Scope" sections, with a one-paragraph charter ready to expand into a full
file when a PR touches the relevant code:

- **`reference-data-integrity.md`** — admin / timer writes blob; every authenticated
  user reads. A tampered `index.json` poisons every browser's instance + spec
  pickers. No content-hash, no signature.
- **`idempotency-replay-cache.md`** — Cosmos-backed `(battleNetId, idempotency-key)`
  replay cache. ID-construction edge cases, `UpsertItemAsync` race on concurrent
  writes, `originalCreatedAt` leakage in replay hint, RU pressure on free-tier.
- **`audit-log-pii-pipeline.md`** — `IActorHasher` HMAC pipeline.
  `AuditOptions.HashSalt` is **not yet wired in `infra/modules/functions.bicep`**,
  so deployed environments emit plaintext `battleNetId` to App Insights. Salt
  rotation breaks historical attribution. `AuditEvent.cs` doc-comment misstates
  GDPR PII status.
- **`character-portrait-fetch.md`** — caller-supplied `(charRegion, realm, name)` →
  cross-region `*.api.blizzard.com/...`. SSRF blocked by allowlist, but legacy
  `PortraitCache` entries are not re-validated; batch amplification not capped.
- **`cors-proxy-trust.md`** — `Origin` allowlist + `X-Forwarded-For` trust scoping.
  Non-browser callers (no `Origin`) bypass CORS; cold-start scale-out resets
  per-instance rate-limit buckets; `TrustedProxyAddresses` IP-format mismatch
  silently falls back.
- **`privacy-contact-anti-scraping.md`** — `/api/privacy-contact/email` returns a
  split address; rate-limited separately. Currently just a one-paragraph note in
  the function's own doc-comment.

## Process: when adding or changing a boundary

1. **Detect**: ask whether the change matches any item in
   "what to threat model" above.
2. **Choose**: edit an existing file if the change is within an existing boundary,
   or add a new file under this directory if it's a new boundary.
3. **Diff**: include the threat-model change in the **same PR** as the code /
   IaC change. Reviewers should reject PRs that match § 3 criteria but lack a
   threat-model diff.
4. **Drift**: if a code reference moves, the line ranges in this directory go
   stale silently. The April 2026 refresh corrected ~50 stale references that had
   accumulated in eight days; the prevention is the per-PR habit above, not a
   periodic sweep.
