# Threat Model: E2E Login Bypass

## Introduction

This document covers the trust boundary at `GET /api/e2e/login` — a deliberate
authentication-bypass endpoint that issues a valid encrypted session cookie for an
arbitrary, caller-supplied `battleNetId` without going through Battle.net OAuth.
The endpoint exists so Playwright E2E tests can stand up a logged-in browser
session against the in-process stack without depending on a real Blizzard account.

The endpoint is gated by **two independent controls**:

1. **Compile gate** — wrapped in `#if E2E ... #endif`. The `E2E` preprocessor symbol
   is only defined when MSBuild is invoked with `E2ETest=true`. Production builds
   do not set the property; the function is not in the deployed binary at all.
2. **Runtime gate** — even when compiled in, the handler returns 404 unless the
   `E2E_TEST_MODE` environment variable equals `"true"`.

Both gates must fail simultaneously for the bypass to be reachable. The threat
model exists to keep that contract visible — a regression in either gate is a full
authentication bypass.

## Data Flow

```
Browser / Test Runner          Functions API                Encrypted Cookie
       |                              |                            |
       |--GET /api/e2e/login?battleNetId=test-bnet-id-admin-->     |
       |                   [boundary: unauthenticated -> arbitrary identity]
       |                              |--#if E2E? (compile-time)   |
       |                              |  no  -> function absent    |
       |                              |  yes -> next gate          |
       |                              |--E2E_TEST_MODE == "true"?  |
       |                              |  no  -> 404 problem+json   |
       |                              |  yes -> next step          |
       |                              |--principal=SessionPrincipal|
       |                              |  (arbitrary battleNetId    |
       |                              |   from query string)       |
       |                              |--cipher.Protect(principal) |
       |<--Set-Cookie: lfm_session----|                            |
       |<--302 to redirect-----------|                            |
```

## Trust Boundaries Crossed

- **Build pipeline → deployable artifact**: setting `E2ETest=true` (or anything
  that injects the `E2E` symbol) controls whether the function class even exists
  in the compiled assembly. CI / CD configuration is part of the trust boundary.
- **Process environment → handler reachability**: `E2E_TEST_MODE` is read from
  `Environment.GetEnvironmentVariable`. App-settings injection is the trust
  boundary.
- **Caller-supplied `battleNetId` → encrypted cookie**: when both gates pass, the
  handler accepts any string as the principal id, including `test-bnet-id-admin`
  (the admin fixture used by E2E tests).

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Caller passes `?battleNetId=test-bnet-id-admin` and is issued a session cookie that satisfies `SiteAdminService` if `test-bnet-id-admin` is in `site-admin-battle-net-ids`. | Both gates must pass. In production neither is intended to be true — the symbol is absent and the env-var is unset. | High **only** if both gates fail; Low under the intended posture. The threat is the gate regression, not the endpoint logic. |
| **Tampering — CI mis-configuration** | A future change to `Lfm.Api.csproj` or a CI step accidentally sets `E2ETest=true` for the production build. The function ships in production; only the env-var stops the bypass. | The `<PropertyGroup Condition="'$(E2ETest)' == 'true'">` predicate is explicit; the property is not set anywhere in `.github/workflows/deploy.yml`. | Medium — a single `dotnet build /p:E2ETest=true` in the wrong workflow defeats the compile gate. Should be linted by a CI assertion that the production build does NOT define `E2E`. |
| **Tampering — env-var mis-injection** | A production app-setting `E2E_TEST_MODE=true` is committed (e.g. via a copy-paste from local dev) or forced via Azure portal override. The runtime gate fails open. | The deploy IaC (`infra/modules/functions.bicep`) does not list `E2E_TEST_MODE` in the `appSettings` block — it cannot be set through Bicep. Manual portal overrides are the only injection path. | Medium — a control-plane action by a privileged operator can flip it. App-setting drift detection on the Functions resource would close this. |
| **Repudiation** | Test traffic from the bypass is indistinguishable from real traffic in App Insights. | The function emits its own structured log entry on each invocation; the issued cookie's `IssuedAt` is the same shape as a normal one. There is no `IsTestPrincipal` field on `SessionPrincipal`. | Low for the test environment (operator knows tests are running). High if the bypass were ever reached in production, since logs would not flag it as anomalous. Acceptable because the prerequisites for production reachability are already covered above. |
| **Information disclosure** | The redirect parameter (`?redirect=...`) accepts any path that starts with `/` OR with `blizzard.AppBaseUrl`. A fully-qualified URL not matching `AppBaseUrl` falls back to `AppBaseUrl`. | Validation is two-armed: relative paths under `/` are accepted as-is; absolute URLs must match `AppBaseUrl`. | Low — the function is dev-only; the validator is consistent with the production callback's rules. |
| **Denial of service** | Unlimited `GET /api/e2e/login` calls in test creating arbitrary cookies. | None within the function. `RateLimitMiddleware` does not list `e2e-login` in `AuthFunctions`, so it falls through to the read tier. | Low for the test environment (intentional). High if the bypass were ever reached in production (unlimited session minting). Acceptable under the gate-not-failed assumption. |
| **Elevation of privilege** | The bypass issues `test-bnet-id-admin`. If `site-admin-battle-net-ids` ever contains `test-bnet-id-admin` in production (operator error), a leaked bypass becomes a privileged session. | Operationally — `site-admin-battle-net-ids` should never contain test fixtures in production. No technical control prevents the operator from setting it. | Medium under operator error; Low under correct operations. Document in the admin runbook. |

## Key Code References

- `api/Lfm.Api.csproj:14-16` — conditional `<DefineConstants>` setting that adds
  the `E2E` symbol only when `E2ETest=true` is passed to MSBuild.
- `api/Functions/E2ELoginFunction.cs:4` — `#if E2E` (compile gate).
- `api/Functions/E2ELoginFunction.cs:24` — `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "e2e/login")]`.
- `api/Functions/E2ELoginFunction.cs:26-29` — runtime gate:
  `if (!string.Equals(Environment.GetEnvironmentVariable("E2E_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase))
  return Problem.NotFound(...)`.
- `api/Functions/E2ELoginFunction.cs:34` — accepts arbitrary `battleNetId` from
  the query string with `"test-bnet-id"` as the default.
- `api/Functions/E2ELoginFunction.cs:37-50` — switches on the id to inject a
  matching `GuildId` / `GuildName` for known fixtures (`test-bnet-id-admin`,
  `test-bnet-id-member`).
- `api/Functions/E2ELoginFunction.cs:52-59` — sets `lfm_session` cookie with
  `Secure = false` (because E2E runs over `http://localhost`).
- `api/Functions/E2ELoginFunction.cs:62-74` — redirect validation; relative paths
  accepted, absolute URLs only when matching `AppBaseUrl`.
- `tests/Lfm.E2E/...` — Playwright tests that drive the endpoint; the only
  intended consumer.

## Out of Scope

- The production OAuth flow that the bypass intentionally circumvents — covered
  by `battlenet-oauth-callback.md`.
- The session cookie shape itself — covered by `session-cookie-api.md`.
- The admin-allowlist secret that determines which test fixture reaches privileged
  paths — covered by `admin-privilege-boundary.md`.
- The Cosmos partition-key isolation that test sessions exercise — covered by
  `cosmos-partition-key-authz.md`. (Test-issued sessions still go through the
  normal partition-key boundary; nothing about the bypass weakens that.)

## Recommended hardening (follow-up tasks, not part of this model)

- Add a CI assertion that `dotnet build` (production target) produces a binary
  whose `E2ELoginFunction` type does not exist — prevents the "compile gate
  silently disabled" regression.
- Add an IaC drift-detection rule that fails if `E2E_TEST_MODE` ever appears in
  the production Function App's `appSettings`.
- Tag the test-fixture session with an `IsTestPrincipal=true` claim so any future
  log scrub can trivially distinguish bypass-issued sessions from real ones.
