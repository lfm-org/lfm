# Security Architecture

## HTTP auth at the Functions trigger layer

**Decision.** Every HTTP-triggered function in `api/Functions/` declares
`AuthorizationLevel.Anonymous`. All authentication and authorization is
enforced in application code by the Battle.net OAuth cookie + the
`[RequireAuth]` attribute + `AuthPolicyMiddleware` + (for admin endpoints)
`ISiteAdminService`.

**Why not `AuthorizationLevel.Function`?** The Functions host's function
keys are the obvious second layer, but they don't fit a browser-based
Blazor WASM SPA:

- The SPA is shipped to every visitor's browser. A function key embedded in
  the bundle or fetched from a config endpoint is extractable, so it is not
  a credential — it's just a token with extra operational cost (rotation,
  leak-response).
- A server-side proxy that injects the key per request would solve the
  extraction problem but adds a tier we don't run in Static Web Apps Free.
  See `docs/storage-architecture.md` for the hosting footprint.
- The app-layer cookie is already derived from user identity (signed,
  encrypted with Data Protection keys in Key Vault). It authenticates the
  specific Battle.net principal, which a host-level key can't do.

**Accepted risk.** A bug in `AuthPolicyMiddleware` or the `[RequireAuth]`
filter leaves the endpoint open to unauthenticated callers — there is no
host-level fallback. Mitigated by:

- Unit tests for the middleware on every PR that touches `api/Middleware/`.
- The 403 path on admin endpoints logs the caller's Battle.net id + the
  ambient `Activity.TraceId` for Application Insights correlation, so
  failed attempts are visible in ops dashboards.
- `ISiteAdminService.IsAdminAsync` checks a small site-admin allowlist
  derived from Battle.net ids; adding a new admin is a deploy-gated action.

**Callers that bypass the HTTP trigger.** Timer triggers
(e.g. `WowReferenceRefreshTimerFunction`) don't go through the HTTP
middleware chain at all — they are host-gated by the Functions runtime.
Ad-hoc master-key invocation (documented on the function itself) is the
ops escape hatch when the cookie flow is unavailable.

## Key Vault secrets — deploy prerequisites

The Functions app reads four secrets from Key Vault (one role grant,
`Key Vault Secrets User`, scoped to the vault). Bicep references them but
does **not** create them — operators must populate the vault before the
first deploy and on rotation. Until a secret exists the Functions runtime
shows the corresponding app setting as "Not Resolved" and falls back to
the in-code default for that setting, which is **not** the secure path.

| Secret name | Bound app setting | Resolver | Effect when missing |
|---|---|---|---|
| `battlenet-client-id` | `Blizzard__ClientId` | Platform `@Microsoft.KeyVault(...)` | Battle.net OAuth + Game Data calls fail at startup. |
| `battlenet-client-secret` | `Blizzard__ClientSecret` | Platform `@Microsoft.KeyVault(...)` | Same. |
| `site-admin-battle-net-ids` | (read at runtime by `KeyVaultSecretResolver`) | `Azure.Security.KeyVault.Secrets.SecretClient` | `SiteAdminService.IsAdminAsync` returns `false` for everyone — admin endpoints become unreachable. Fail-closed. |
| `audit-hash-salt` | `Audit__HashSalt` | Platform `@Microsoft.KeyVault(...)` | `AuditLog` falls back to `IdentityActorHasher` and emits **plaintext** `battleNetId` (PII) into Application Insights. **Fail-open.** |

Generate `audit-hash-salt` with `openssl rand -base64 32` and store in the
`audit-hash-salt` secret. Rotation breaks linkage of historical audit
events to live ones, so rotate only when there is reason to.

## Related documents

- `docs/wire-payload-contract.md` — what leaves the API boundary.
