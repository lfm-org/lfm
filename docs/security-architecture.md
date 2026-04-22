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

## Related documents

- `docs/threat-models/session-cookie-api.md` — cookie → API threat model.
- `docs/threat-models/battlenet-oauth-callback.md` — OAuth callback threat model.
- `docs/wire-payload-contract.md` — what leaves the API boundary.
