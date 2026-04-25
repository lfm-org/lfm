# Threat Model: Session Cookie to API Endpoints

## Introduction

This document covers the trust boundary between a browser holding an encrypted
`lfm_session` cookie and the `[RequireAuth]`-decorated API endpoints that serve
personalized data. An attacker who can read, forge, or replay that cookie gains
authenticated access as the victim; an attacker who can bypass the auth check gains
access to protected resources without any credentials at all. The session cookie is the
sole credential for all authenticated API calls.

## Repo-wide auth pattern (read this first)

Every Function in `api/Functions/*.cs` declares `AuthorizationLevel.Anonymous` at the
host trigger. **This is intentional and is not an anonymous endpoint.** Authentication
and authorization are enforced inside the worker by the middleware chain, specifically:

1. `AuthMiddleware` — decrypts the `lfm_session` cookie, validates expiry, and stores
   the resulting `SessionPrincipal` in `HttpContext.Items[SessionKeys.Principal]`.
2. `AuthPolicyMiddleware` — checks for `[RequireAuth]` on the function method or class
   via cached reflection; returns 401 problem+json if a principal is required and
   absent.

Any reviewer scanning for `AuthorizationLevel.Function` / `Admin` etc. will find none;
that is the deliberate pattern, not a SAD-G-anonymous-private finding. Three endpoints
are *genuinely* anonymous (login, callback, logout, health, privacy-contact, the
CORS preflight catch-all, and the E2E-only login bypass when compiled in); every other
function is gated by `[RequireAuth]`.

## Data Flow

```
Browser                    Functions API (middleware chain)         Cosmos DB
  |                              |                                      |
  |--GET /api/me (cookie)------->|                                      |
  |                   [boundary: browser session -> API trust zone]
  |                              |--CorsMiddleware (origin check)       |
  |                              |--SecurityHeadersMiddleware           |
  |                              |--RequestSizeLimitMiddleware (413)    |
  |                              |--RateLimitMiddleware (429)           |
  |                              |--AuditMiddleware (timing only)       |
  |                              |--AuthMiddleware (Unprotect cookie)   |
  |                              |--AuthPolicyMiddleware (RequireAuth?) |
  |                              |    if no principal -> 401            |
  |                              |--IdempotencyMiddleware (replay?)     |
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
| **Spoofing** | Attacker forges a session cookie to log in as another user. | `DataProtectionSessionCipher.Unprotect` uses AEAD via `IDataProtector`; forgery without the key is infeasible. Key is wrapped by Key Vault. | Low — depends on key confidentiality (see `keyvault-data-protection-wrap.md`). |
| **Tampering** | Attacker replays an expired but otherwise valid cookie. | `AuthMiddleware` checks `principal.ExpiresAt + ClockSkew > UtcNow` after unprotecting, where `ClockSkew = 30s`. State-changing endpoints additionally honour `If-Match` ETags (`PUT /runs/{id}`, `PATCH /me`, `PATCH /guild`) so a replayed cookie within the skew window cannot bypass optimistic concurrency. | Low — replay window is bounded to the configured 30 s skew; `If-Match` prevents lost-update on concurrent writes. |
| **Repudiation** | Attacker denies having made a state-changing API call. | `AuditMiddleware` records function name, invocation id, and elapsed time on every request. Per-handler `AuditLog.Emit` records mutating actions; `IActorHasher` HMAC-hashes the actor id when `AuditOptions.HashSalt` is set. `Audit__HashSalt` is wired in Bicep from the `audit-hash-salt` Key Vault secret (`docs/security-architecture.md`). App Insights + Log Analytics retain logs. | Medium — `AuditMiddleware` does **not** log the principal identity; only per-handler `AuditLog.Emit` calls capture the actor, and the actor is hashed. App Insights ingestion is best-effort. If the operator has not populated `audit-hash-salt`, runtime falls back to plaintext (fail-open); see `audit-log-pii-pipeline.md` (backlog). |
| **Information disclosure** | XSS in the frontend steals the session cookie. | Cookie is `HttpOnly=true`. `SecurityHeadersMiddleware` adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Strict-Transport-Security`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'`, plus build-id headers `X-Source-Code` and `X-Source-Commit`, plus a default `Cache-Control: private, no-store` fallback for any handler that did not set its own. Every problem+json error body carries a `traceId` extension. | Medium — CSP on the API response is restrictive; the Blazor WASM SPA's own CSP headers are managed by Static Web Apps and are not visible in this repo. The `traceId` exposed in error responses is a non-secret W3C trace identifier; it is a minor request fingerprint, not a credential. |
| **Denial of service** | Attacker sends many requests with invalid cookies to force repeated `Unprotect` operations, or floods the API with oversized payloads. | `RequestSizeLimitMiddleware` (default 64 KiB cap, 413 problem+json) runs ahead of rate-limit, auth, and handler — bounding compute exposure on oversized inputs. `RateLimitMiddleware` runs next; 429 responses include `Retry-After: 60`. `X-Forwarded-For` is only honoured from configured `TrustedProxyAddresses`, so direct callers cannot forge fresh per-IP buckets. | Medium — per `serverless-api-design §3.10`, edge rate-limiting (Front Door / API Management) is the **primary** defence; the in-process middleware here is a fallback. SWA Free tier provides no edge rate-limit, so this is an accepted trade-off for the current cost stance. `RateLimit-Limit` / `RateLimit-Remaining` / `RateLimit-Reset` informational headers are not emitted on success responses (§3.10 deviation; minor for a hobby project with first-party clients only). |
| **Elevation of privilege** | Attacker bypasses `[RequireAuth]` by reaching a function handler before `AuthPolicyMiddleware` runs. | `AuthPolicyMiddleware` runs immediately before the handler. `IdempotencyMiddleware` runs after `AuthPolicyMiddleware` and only operates on already-authenticated callers (anonymous requests bypass idempotency state writes entirely). Reflection-based attribute lookup is cached in a `ConcurrentDictionary` to avoid per-request overhead. | Low — registration order in `Program.cs:20-30` keeps `AuthPolicyMiddleware` immediately before the handler; risk materialises only if registration order is changed. |

## Key Code References

- `api/Middleware/AuthMiddleware.cs:18` — `ClockSkew = TimeSpan.FromSeconds(30)`
  constant; consumed by the expiry check.
- `api/Middleware/AuthMiddleware.cs:22-36` — reads the session cookie by configured
  name, calls `cipher.Unprotect`, validates `ExpiresAt + ClockSkew > UtcNow`, and
  stores the principal in `context.Items`.
- `api/Middleware/AuthPolicyMiddleware.cs:16-28` — checks `context.TryGetPrincipal()`;
  returns 401 problem+json if null and the function is decorated with `[RequireAuth]`.
- `api/Middleware/AuthPolicyMiddleware.cs:30-43` — reflection-based attribute lookup
  cached in `ConcurrentDictionary` to avoid per-request reflection overhead.
- `api/Middleware/SecurityHeadersMiddleware.cs:41-58` — adds all response headers.
  Lines 41-47 emit `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`,
  `Strict-Transport-Security`, `Content-Security-Policy`, plus build-id headers
  `X-Source-Code`, `X-Source-Commit`. Lines 55-58 add a default
  `Cache-Control: private, no-store` for any response that did not set its own.
- `api/Middleware/RequestSizeLimitMiddleware.cs` — 64 KiB body cap; emits 413
  problem+json with `traceId`.
- `api/Middleware/RateLimitMiddleware.cs:21-22` — `AuthFunctions` set governing the
  strict auth-tier ceiling.
- `api/Middleware/RateLimitMiddleware.cs:114` — `Retry-After: 60` on 429 response.
- `api/Middleware/RateLimitMiddleware.cs:124-147` — `GetClientIp` honours
  `X-Forwarded-For` only from `TrustedProxyAddresses`.
- `api/Middleware/IdempotencyMiddleware.cs` — runs after `AuthPolicyMiddleware`; the
  replay cache is partitioned per `battleNetId` derived from the session principal,
  not from request parameters.
- `api/Auth/DataProtectionSessionCipher.cs:11-12` — cipher creates the
  `IDataProtector` with purpose string `Lfm.Session.v1`; `Protect` serialises the
  principal to JSON then AEAD-encrypts it.
- `api/Auth/SessionPrincipal.cs:13-17` — `AccessToken` field stores the Battle.net
  OAuth token inside the encrypted session cookie; leaking the cookie exposes the
  token (see `blizzard-outbound-api.md`).
- `api/Auth/RequireAuthAttribute.cs:10-11` — marker attribute targeting method or
  class; `Inherited = true` so subclasses inherit the requirement.
- `api/Program.cs:20-30` — canonical middleware registration order
  (CORS → SecurityHeaders → RequestSizeLimit → RateLimit → Audit → Auth → AuthPolicy
  → Idempotency). Must be preserved to maintain the security chain.
- `api/Program.cs:42-52` — `AddProblemDetails` registration; every problem+json body
  includes the W3C `traceId` extension via the customizer.
- `api/Program.cs:254-276` — Data Protection key ring configured with KV wrap and
  blob persistence; session cookie integrity depends on this key ring.
- `api/Program.cs:286-287` — `AuditLog.ConfigureHasher` installs the DI-selected
  `IActorHasher`.

## Out of Scope

- Cosmos-level authorization (which documents a principal can read/write) — covered by
  `cosmos-partition-key-authz.md`.
- Key Vault key compromise and Data Protection key rotation — covered by
  `keyvault-data-protection-wrap.md`.
- The OAuth flow that issues the cookie in the first place — covered by
  `battlenet-oauth-callback.md`.
- `IdempotencyMiddleware` / `IdempotencyStore` semantics, TTL, and replay correctness
  — covered by `idempotency-replay-cache.md` (backlog).
- E2E test-only login bypass — covered by `e2e-login-bypass.md`. The bypass is
  compile-guarded behind `#if E2E` and runtime-gated by `E2E_TEST_MODE=true`; it
  does not exist in production binaries.
