# Threat Model: Battle.net OAuth Callback

## Introduction

This document covers the trust boundary between an unauthenticated browser and the
`GET /api/battlenet/callback` endpoint that completes the Battle.net PKCE
authorization-code flow and issues the application session cookie. An attacker who
can influence this boundary before the session is established may attempt to forge a
login, replay a stolen authorization code, fix the session to a known value, redirect
the user to a malicious host, or gain a valid session for a victim account.

## Data Flow

```
Browser                    Functions API               Battle.net OAuth
  |                              |                            |
  |--GET /api/battlenet/login--->|                            |
  |                              |--GenerateState()+PKCE----->|
  |<--Set-Cookie: login_state----|                            |
  |                    [boundary: unauthenticated -> Battle.net]
  |<--302 to battle.net/authorize|                            |
  |----user authorizes------------------------>               |
  |<----302 /callback?code=X&state=Y-----------               |
  |                    [boundary: Battle.net response -> public callback]
  |--GET /api/battlenet/callback?code=X&state=Y->             |
  |                              |--ExchangeCodeAsync(code, verifier)->
  |                              |<--access_token--------------|
  |                              |--GetUserInfoAsync(token)--->|
  |                              |<--{id, battletag}-----------|
  |                              |--UpsertRaider()             |
  |<--Set-Cookie: lfm_session----|                            |
  |<--302 to AppBaseUrl{redirect}|                            |
```

## Trust Boundaries Crossed

- **Browser to callback endpoint**: `code` and `state` arrive as unauthenticated query
  parameters from a browser redirect; the server cannot verify the HTTP origin.
- **login_state cookie to server**: the encrypted PKCE state payload crosses from the
  client to the server; cookie integrity depends on the Data Protection key ring.
- **Callback endpoint to Battle.net token endpoint**: the server presents `code` +
  `codeVerifier` + client credentials to an external OAuth server over HTTPS.
- **Battle.net user-info to Cosmos**: the `id` and `battletag` returned by Battle.net
  are written directly into the `raiders` container without additional input sanitization.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Attacker submits a stolen `code` without the corresponding `login_state` cookie. | State is sealed with `IDataProtector` (purpose `Lfm.OAuth.LoginState.v1`); mismatch → `RejectWithClearedCookie`. PKCE `codeVerifier` is bound to the code. | Low — stolen code is useless without the cookie, which is `HttpOnly`; PKCE prevents server-side code injection. |
| **Tampering** | Attacker modifies the `state` query parameter or the `login_state` cookie. | `login_state` payload is authenticated-encrypted by `IDataProtector`; any tampering causes `Unprotect` to throw → reject. State comparison is strict equality (`loginState.Value.state != urlState`). | Low — AEAD protects integrity; SHA-256 comparison is not vulnerable to timing attacks in the reject path. |
| **Repudiation** | Attacker denies having logged in or disputes which account authenticated. | `AuditLog.Emit` writes `login.success` and `login.failure` events including `battleNetId`; App Insights + Log Analytics retain them. | Low — structured audit trail exists. No non-repudiation signature on individual events. |
| **Information disclosure** | Error paths leak state details or the authorization code to attacker-controlled infrastructure. | All error paths call `RejectWithClearedCookie`, which redirects to `/auth/failure` and logs only a generic `detail` string internally. `code` and tokens are never echoed to the client. | Low — no 200-body response on error; `detail` string is not in the redirect URL. |
| **Denial of service** | Attacker floods `/api/battlenet/callback` to exhaust token-exchange quota with Battle.net or trigger Functions cold-starts. | `RateLimitMiddleware` is registered in the pipeline ahead of this function. | Medium — rate limit is in-memory per-instance; distributed DoS still possible. Battle.net has its own rate limits on the token endpoint. |
| **Elevation of privilege** | Open-redirect via the `redirect` post-login parameter allows attacker to redirect victim to phishing site after authentication. | `BattleNetLoginFunction.IsValidRedirect` enforces the redirect must start with `/` and must not start with `//` (protocol-relative). Final redirect is always `{AppBaseUrl}{redirect}`, so destination is bounded by the API's configured `AppBaseUrl`. | Low — the restriction is explicit and unit-testable. Absolute-URL payloads are silently dropped. |

## Key Code References

- `api/Functions/BattleNetCallbackFunction.cs:54-60` — checks for missing `login_state`
  cookie and state query param; rejects if either is absent or state does not match.
- `api/Functions/BattleNetCallbackFunction.cs:71` — PKCE code exchange: passes
  `codeVerifier` from the decrypted cookie to `ExchangeCodeAsync`.
- `api/Functions/BattleNetCallbackFunction.cs:118-125` — session cookie set with
  `HttpOnly`, `Secure`, `SameSite=Lax`.
- `api/Functions/BattleNetCallbackFunction.cs:133-136` — post-login redirect bounded
  by `AppBaseUrl + postLoginRedirect`.
- `api/Functions/BattleNetLoginFunction.cs:63-66` — `IsValidRedirect` guard rejecting
  protocol-relative and absolute-URL redirect values.
- `api/Services/BlizzardOAuthClient.cs:105-113` — `ProtectLoginState` seals state +
  verifier into an AEAD payload using `IDataProtector`.
- `api/Services/BlizzardOAuthClient.cs:117-139` — `UnprotectLoginState` verifies
  integrity; returns `null` on any tamper or expiry.
- `api/Services/BlizzardOAuthClient.cs:202-206` — `ComputeCodeChallenge` implements
  the PKCE S256 transform: `base64url(SHA-256(verifier))`.
- `api/Program.cs:16-21` — middleware registration order: CORS, security headers,
  rate limit, audit, auth, auth-policy.

## Out of Scope

- Token storage after the session cookie is issued (covered by `session-cookie-api.md`).
- Battle.net account takeover or vulnerabilities in Battle.net's own OAuth server.
- PKCE downgrade attacks at the Battle.net authorization server (outside our control);
  we always send `code_challenge_method=S256`.
