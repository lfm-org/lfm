# Threat Model: Blizzard Outbound API

## Introduction

This document covers the trust boundary between the Functions app and Blizzard's
external APIs: the OAuth endpoints (`*.battle.net/oauth/...`) and the Profile + Game
Data APIs (`*.api.blizzard.com/...`). Three flows cross this boundary:

1. **Authorization Code + PKCE** for end-user login (covered in
   `battlenet-oauth-callback.md`); the resulting `access_token` is forwarded to
   Profile API calls on the user's behalf.
2. **Client Credentials** for the Game Data API used by `WowReferenceRefreshFunction`
   and the timer; `BlizzardGameDataClient` requests a fresh token per `SyncAllAsync`
   invocation using `Blizzard__ClientId` + `Blizzard__ClientSecret`.
3. **Profile API requests with the user's access token** for character lists,
   character refresh, and portrait fetches; the token comes from
   `SessionPrincipal.AccessToken` inside the encrypted session cookie.

An attacker who can intercept these flows, exfiltrate the long-lived client secret,
manipulate Blizzard's response to throttle the entire LFM API, or extract a user's
WoW access token from a stolen session cookie can forge upstream calls, deny
service to all users, or read the victim's WoW profile data.

## Data Flow

```
Functions Instance              Blizzard OAuth                Blizzard API
       |                              |                            |
       | A. Authorization Code (per-user)                           |
       |--POST /oauth/token (Basic clientId:clientSecret)---------->|
       |<--access_token (user-scoped)-------------------------------|
       | (token is then stored INSIDE the encrypted session cookie) |
       |                                                            |
       | B. Profile API (per-user)                                  |
       |--BlizzardRateLimiter.Acquire()                             |
       |--GET /profile/user/wow + Authorization: Bearer <userToken>--|
       |<--profile JSON--------------------------------------------|
       |                                                            |
       | C. Client Credentials (admin/timer)                        |
       |--POST /oauth/token grant_type=client_credentials----------->|
       |  Basic clientId:clientSecret                               |
       |<--access_token (app-scoped)--------------------------------|
       |                                                            |
       | D. Game Data API (admin/timer)                             |
       |--BlizzardRateLimiter.Acquire()                             |
       |--GET /data/wow/... + Authorization: Bearer <appToken>------>|
       |<--reference JSON-------------------------------------------|
       |                                                            |
       | E. Throttled (any flow)                                    |
       |<--429 + Retry-After: <seconds>-----------------------------|
       |--BlizzardRateLimitHandler reads Retry-After                |
       |--limiter.PauseUntil(now + retryAfter)                      |
       |  (pauses the SHARED limiter for ALL outbound traffic)      |
```

## Trust Boundaries Crossed

- **Functions MI / app secrets → Blizzard OAuth**: client credentials live in Key
  Vault and are injected via `@Microsoft.KeyVault(...)` references. The
  client-credentials flow trades them for a short-lived app token at every
  `SyncAllAsync` invocation.
- **User session cookie → Blizzard Profile API**: `SessionPrincipal.AccessToken` is
  serialised into the encrypted session cookie. Every Profile API request inside an
  authenticated handler reads the token from the principal and forwards it as a
  Bearer header.
- **External API responses → in-process rate limiter**: `BlizzardRateLimitHandler`
  reads upstream `Retry-After` headers from 429 responses and applies them via
  `IBlizzardRateLimiter.PauseUntil`. The limiter is process-wide.
- **Functions instance → Blizzard regional endpoints**: outbound calls target
  `https://{region}.battle.net/...` or `https://{region}.api.blizzard.com/...`,
  selected from `Blizzard__Region` plus per-character overrides for cross-region
  portrait fetches.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker exfiltrates `Blizzard__ClientSecret` and impersonates the LFM app for client-credentials calls. | Stored in Key Vault and resolved by the Functions platform via `@Microsoft.KeyVault(...)` references; no in-code literal. The same `Key Vault Secrets User` role grant on the Functions MI is the only read path. | Medium — secret is long-lived (rotated on operator demand, not automatically). A Functions MI compromise reveals it; rotation cadence is undocumented. The same secret powers both the user PKCE flow and the admin client-credentials flow, so rotation must coordinate both. |
| **Tampering — adversarial Retry-After** | Blizzard (or a network path positioned between LFM and Blizzard) returns a 429 with `Retry-After: 999999` (or a future `Retry-After` Date), pausing `BlizzardRateLimiter` for far longer than intended. Every outbound call from every authenticated user goes through this single shared limiter. | `BlizzardRateLimitHandler.SendAsync` falls back to `TimeSpan.FromSeconds(1)` only when both `Delta` and `Date` parsing fail — *any* parseable upstream value is honoured verbatim. | **Medium-High** — exploitable as a self-inflicted DoS amplifier whenever the upstream is degraded. Bound the maximum pause (e.g., `Math.Min(retryAfter, MaxPause)` with `MaxPause = 60s`); flag as a code-fix follow-up. |
| **Repudiation** | LFM cannot prove an outbound Blizzard request was made by us versus an attacker who stole `ClientSecret`. | Application Insights captures every outbound dependency call with the W3C `traceparent` header (the `IHttpClientFactory` typed clients propagate `Activity.Current`). Blizzard's own audit trail records the calling client id. | Low — within LFM's logs, every outbound call is correlated to an inbound trace. Outside LFM (post-secret-exfiltration replay), only Blizzard's logs distinguish the actor. |
| **Information disclosure — user access token in cookie** | An attacker who steals an `lfm_session` cookie obtains the embedded WoW `AccessToken` and reads the victim's WoW profile data directly from Blizzard, bypassing LFM entirely. | Cookie is `HttpOnly`, AEAD-encrypted, and bound to the DP key ring (see `session-cookie-api.md` and `keyvault-data-protection-wrap.md`). The cookie is not transmitted on cross-site requests (`SameSite=Lax`). | Medium — XSS or any code path that exfiltrates the cookie also exposes the WoW token. The token's lifetime tracks Blizzard's issuance (24 h by default) — short, but not zero. The session cookie's `MaxAge` exceeds the token's; an expired token in a still-valid cookie returns Blizzard 401 on profile calls. |
| **Information disclosure — client_credentials token caching** | `BlizzardGameDataClient.GetClientCredentialsTokenAsync` requests a new token on every `SyncAllAsync` rather than caching for the token's `expires_in` window. Token is exchanged on the wire each invocation. | TLS protects the token in flight. The token is never persisted server-side; only held for the duration of the sync. | Low-Medium — extra outbound traffic, marginally larger attack window for in-flight interception. Acceptable for the current invocation cadence (manual or weekly). Cache to KV-resolved `IDistributedCache` if invocation frequency rises. |
| **Denial of service — outbound throttle starvation** | Blizzard rate-limits LFM's app id (100 req/s per app); concurrent Functions instances each run their own `BlizzardRateLimiter` configured for ~80 req/s and have no cross-instance coordination. Under scale-out, the combined outbound rate exceeds the upstream cap, triggering per-instance 429s and shared `PauseUntil` calls. | `BlizzardRateLimiter` is a singleton **per Functions instance**, not per app. Each instance throttles itself locally; the combined fleet does not. | Medium — under multi-instance scale-out the outbound budget is over-spent and the resulting 429s degrade every user. The free-tier plan limits scale-out so this is a depth-in-defence concern, not exploitable today. Distributed limiter (e.g., Redis-backed) would close this if scale-out becomes routine. |
| **Elevation of privilege** | Attacker persuades the user to authorise a wider `scope` set during PKCE login, then uses the resulting token against richer Profile API endpoints. | `BattleNetLoginFunction` sends `scope=wow.profile` only; the user-consent screen on Blizzard reflects exactly that scope. | Low — request scope is hard-coded server-side; an attacker cannot alter it without compromising the Functions binary or its config. |

## Key Code References

- `api/Services/BlizzardOAuthClient.cs:108-117` — `ProtectLoginState` (PKCE flow A
  setup; covered in detail by `battlenet-oauth-callback.md`).
- `api/Services/BlizzardOAuthClient.cs:145-178` — `ExchangeCodeAsync` — Basic-auth
  POST to the Battle.net token endpoint with the user's authorization code.
- `api/Services/BlizzardOAuthClient.cs:180-198` — `GetUserInfoAsync` — Bearer-auth
  GET to the userinfo endpoint with the user's token.
- `api/Services/BlizzardGameDataClient.cs:44-68` — `GetClientCredentialsTokenAsync`
  — POST `grant_type=client_credentials` with `Authorization: Basic
  base64(clientId:clientSecret)`. No token caching across calls.
- `api/Services/BlizzardProfileClient.cs` — Bearer-auth GETs to the Profile API
  using the user's token.
- `api/Services/CharacterPortraitService.cs` — fetches from
  `https://{charRegion}.api.blizzard.com/...` (see `character-portrait-fetch.md`,
  backlog, for the SSRF / region allowlist details).
- `api/Services/BlizzardRateLimiter.cs:36` — `PauseUntil(DateTimeOffset until)`;
  shared in-process limiter.
- `api/Services/BlizzardRateLimitHandler.cs:16-36` — `SendAsync` honours upstream
  `Retry-After` (Delta or Date) verbatim with no upper bound; pauses the shared
  limiter on any 429 from Blizzard.
- `api/Program.cs:195-196` — `BlizzardRateLimiter` registered as singleton;
  `BlizzardRateLimitHandler` registered as transient `DelegatingHandler`.
- `api/Program.cs:201-237` — `IHttpClientFactory` registrations for
  `CharacterPortraitService`, `BlizzardOAuthClient`, `BlizzardProfileClient`,
  `BlizzardGameDataClient`. All chain `BlizzardRateLimitHandler` and
  `AddStandardResilienceHandler` (retry + circuit breaker + timeout per
  `Microsoft.Extensions.Http.Resilience`).
- `api/Auth/SessionPrincipal.cs:13-17` — `AccessToken` field embedded in the
  encrypted session cookie; the value forwarded to Profile API calls.
- `infra/modules/functions.bicep:111-112` — `Blizzard__ClientId` and
  `Blizzard__ClientSecret` resolved via `@Microsoft.KeyVault(...)` references.

## Out of Scope

- The PKCE login + callback flow itself — covered by `battlenet-oauth-callback.md`.
- Storage of the user's access token in the session cookie — covered by
  `session-cookie-api.md`.
- The Key Vault role grant that resolves `Blizzard__ClientSecret` — covered by
  `keyvault-data-protection-wrap.md`.
- Admin authorization for the timer / refresh function that drives the
  client-credentials flow — covered by `admin-privilege-boundary.md`.
- The downstream effect of a tampered Game Data response on every browser (manifests
  in blob serving wrong instance / spec data) — covered by
  `reference-data-integrity.md` (backlog).
- Cross-region URL construction in `CharacterPortraitService` and the SSRF /
  cache-poisoning surface — covered by `character-portrait-fetch.md` (backlog).
