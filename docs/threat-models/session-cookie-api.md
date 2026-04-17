# Threat Model: Session Cookie to API Endpoints

## Introduction

This document covers the trust boundary between a browser holding an encrypted
`lfm_session` cookie and the `[RequireAuth]`-decorated API endpoints that serve
personalized data. An attacker who can read, forge, or replay that cookie gains
authenticated access as the victim; an attacker who can bypass the auth check gains
access to protected resources without any credentials at all. The session cookie is the
sole credential for all authenticated API calls.

## Data Flow

```
Browser                    Functions API (middleware chain)         Cosmos DB
  |                              |                                      |
  |--GET /api/me (cookie)------->|                                      |
  |                   [boundary: browser session -> API trust zone]
  |                              |--CorsMiddleware (origin check)       |
  |                              |--SecurityHeadersMiddleware           |
  |                              |--RateLimitMiddleware                 |
  |                              |--AuditMiddleware                     |
  |                              |--AuthMiddleware (Unprotect cookie)   |
  |                              |--AuthPolicyMiddleware (RequireAuth?) |
  |                              |    if no principal -> 401            |
  |                              |--Function handler (uses principal)   |
  |                              |--ReadItemAsync(battleNetId, pk)----->|
  |                              |<--RaiderDocument--------------------|
  |<--200 {raider data}----------|                                      |
```

## Trust Boundaries Crossed

- **Browser to Functions runtime**: the encrypted cookie crosses from the browser over
  HTTPS; its integrity relies on the Data Protection key ring backed by Key Vault.
- **Middleware chain to function handler**: `AuthMiddleware` populates
  `context.Items[SessionKeys.Principal]`; `AuthPolicyMiddleware` enforces that it is
  non-null for `[RequireAuth]` endpoints. Any middleware ordering error collapses
  this boundary.
- **Function handler to Cosmos**: the handler uses `principal.BattleNetId` directly as
  the Cosmos partition key; any corruption of the principal bypasses user isolation.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker forges a session cookie to log in as another user. | `DataProtectionSessionCipher.Unprotect` uses AEAD (AES-256-CBC + HMAC-SHA256 by default) via `IDataProtector`; forgery without the key is infeasible. Key is wrapped by Key Vault. | Low — depends on key confidentiality (see `keyvault-data-protection-wrap.md`). |
| **Tampering** | Attacker replays an expired but otherwise valid cookie. | `AuthMiddleware` checks `principal.ExpiresAt > DateTimeOffset.UtcNow` after unprotecting; expired sessions are silently dropped. | Low — clock-skew window is negligible (UTC comparison, no tolerance added). |
| **Repudiation** | Attacker claims they did not make a state-changing API call. | `AuditMiddleware` is registered in the pipeline ahead of function handlers and logs request method, path, and (after auth) the principal identity. App Insights + Log Analytics retain logs. | Medium — audit events are best-effort; if App Insights ingestion is delayed or dropped, the record is lost. No tamper-evident log store. |
| **Information disclosure** | XSS in the frontend steals the session cookie. | Cookie is set `HttpOnly=true`, preventing JavaScript access. `SecurityHeadersMiddleware` adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and a restrictive `Content-Security-Policy`. `Strict-Transport-Security` prevents HTTP downgrade. | Medium — CSP on the API response is `default-src 'none'; frame-ancestors 'none'`, but the Blazor WASM SPA's own CSP headers are managed by Static Web Apps and are not visible in this repo; a weak SPA CSP could enable XSS on the frontend origin. |
| **Denial of service** | Attacker sends many requests with invalid cookies to force repeated `Unprotect` operations. | `RateLimitMiddleware` is registered before `AuthMiddleware` and can throttle per-IP. | Medium — rate limiter is in-process; under heavy load from many IPs it provides limited protection. |
| **Elevation of privilege** | Attacker bypasses `[RequireAuth]` by reaching a function handler before `AuthPolicyMiddleware` runs. | `AuthPolicyMiddleware` is the last middleware before the handler; it uses reflection-based caching to check for `RequireAuthAttribute` on the method or class. 401 is returned immediately if principal is absent. | Low — middleware is registered in correct order in `Program.cs`. Risk materialises only if registration order is changed. |

## Key Code References

- `api/Middleware/AuthMiddleware.cs:17-24` — reads the session cookie by configured
  name, calls `cipher.Unprotect`, validates `ExpiresAt`, and stores the principal in
  `context.Items`.
- `api/Middleware/AuthPolicyMiddleware.cs:15-23` — checks `context.TryGetPrincipal()`;
  returns 401 if null and the function is decorated with `[RequireAuth]`.
- `api/Middleware/AuthPolicyMiddleware.cs:27-40` — reflection-based attribute lookup
  cached in `ConcurrentDictionary` to avoid per-request reflection overhead.
- `api/Middleware/SecurityHeadersMiddleware.cs:22-26` — adds `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Strict-Transport-Security`, and `CSP` headers
  to every HTTP response.
- `api/Auth/DataProtectionSessionCipher.cs:8-14` — cipher creates the `IDataProtector`
  with purpose string `Lfm.Session.v1`; `Protect` serializes the principal to JSON then
  AEAD-encrypts it.
- `api/Auth/SessionPrincipal.cs:11-14` — `AccessToken` field stores the Battle.net
  OAuth token inside the encrypted session cookie; leaking the cookie exposes the token.
- `api/Auth/RequireAuthAttribute.cs:7-8` — marker attribute targeting method or class;
  `Inherited = true` so subclasses inherit the requirement.
- `api/Program.cs:16-21` — canonical middleware registration order that must be
  preserved to maintain the security chain.
- `api/Program.cs:167-178` — Data Protection key ring configured with KV wrap and blob
  persistence; session cookie integrity depends on this key ring.

## Out of Scope

- Cosmos-level authorization (which documents a principal can read/write) — covered by
  `cosmos-partition-key-authz.md`.
- Key Vault key compromise and Data Protection key rotation — covered by
  `keyvault-data-protection-wrap.md`.
- The OAuth flow that issues the cookie in the first place — covered by
  `battlenet-oauth-callback.md`.
