# E2E Maintenance

E2E tests are reserved for behavior that only a real browser and full local
stack can prove. Prefer unit, API, app-core, or bUnit tests when those lanes can
prove the same contract.

## Local Stack

The E2E fixture owns the full local runtime. Testcontainers starts Cosmos DB,
Azurite, and the API container built from `api/Dockerfile`; the Blazor
`wwwroot` publish output is served by an in-process Kestrel host. The API
container reaches Cosmos DB and Azurite through shared Docker network aliases,
not host-local emulator URLs. A running Docker engine is required, but
host-local Azure Functions Core Tools (`func`) is not.

Runtime publish output lives under `.cache/e2e-runtime/`, and normal fixture
disposal removes its own run directory. Diagnostics are written under
`artifacts/e2e-results/` so failed runs do not leave tracked-file pollution.

## Lanes

| Lane | Use E2E for | Move down when |
|------|-------------|----------------|
| Functional | Multi-step user journeys across browser, SPA, API, storage, and redirects | A single component, DTO, API status, or selector proves the behavior |
| Accessibility | axe scans and keyboard/focus states that need browser rendering | Static labels or component markup can be checked in bUnit |
| Security | Browser-enforced CORS, CSP, iframe, cookie, and redirect behavior | Server-only status/header logic can be tested in API tests |
| Auth flow | Real redirect/callback/session behavior | `/api/e2e/login` is enough and the browser adds no signal |
| Performance | Browser Web Vitals-style lab metrics, route interaction timing, resource/API counts, and local request-health probes | A bundle check, API operation-count assertion, or production telemetry query proves the same regression |

Auth-sensitive E2E tests should use the Testcontainers-managed OAuth provider
and drive the app's real sign-in button, provider authorize page, callback,
session cookie, and post-callback `/api/v1/me` probe. Use `/api/e2e/login`
only when authentication is incidental setup for a non-auth browser journey.

Performance E2E reports local lab signals, not production SLOs. Browser metrics
and local load-smoke timing can fail only at documented hybrid gates; production
timing remains advisory unless promoted by a separate issue with enough
baseline evidence.

## Required Test Comment

Place this compact comment immediately above each new E2E test method:

```csharp
// E2E scope: proves <browser-observable behavior>.
// Cheaper lanes cannot prove this because <reason>.
// Shared data: <none|read-only|disposable|restored>.
```

## Shared Data Rule

No E2E test may permanently mutate or delete shared seed records such as the
primary raider, secondary raider, seeded guild, or seeded run. Destructive tests
must use disposable seed records created for that scenario.

## Drift Points

Centralize these in helpers rather than specs:

- API route strings: helper/client classes.
- Auth/login URLs: `tests/Lfm.E2E/Helpers/AuthHelper.cs`.
- Selectors: page objects under `tests/Lfm.E2E/Pages/`.
- Cosmos seed shapes: typed builders under `tests/Lfm.E2E/Seeds/`.
- Artifact naming and capture: `tests/Lfm.E2E/Infrastructure/E2ETestBase.cs`.
- Performance metric injection, aggregation, and thresholds:
  `tests/Lfm.E2E/Helpers/PerformanceMetricsHelper.cs`.

## Drift Audit

Run the drift script after UI, API route, auth-helper, selector, or
storage-shape changes:

```bash
./scripts/check-e2e-drift.sh
```

The script hard-fails on stale app API routes, stale selectors, and raw raider
seed wire keys. It also prints review-only hits for extra browser pages and
destructive flows so the maintainer can confirm diagnostics and data isolation.

The underlying checks are intentionally scoped:

```bash
rg -n "\"/api/(battlenet|wow|raiders|runs|guilds|me)(/|[?\"])" tests/Lfm.E2E/Specs tests/Lfm.E2E/Helpers tests/Lfm.E2E/Pages
rg -n "wow_accounts|playable_class" tests/Lfm.E2E/Seeds/DefaultSeed.cs tests/Lfm.E2E/Seeds/RaiderSeedBuilder.cs
rg -n "modekey-input|#instance-select" tests/Lfm.E2E/Specs
rg -n "modekey-input|#instance-select" tests/Lfm.E2E/Pages
rg -n "\\bvar [A-Za-z0-9_]+ = await .*NewPageAsync\\(" tests/Lfm.E2E/Specs
rg -n "DeleteAsync|DELETE|me-delete|DeleteAccount" tests/Lfm.E2E/Specs tests/Lfm.E2E/Seeds
```

Expected:

- E2E specs/helpers do not hardcode stale unversioned app API paths. App API
  calls use `/api/v1/...`; the test-only auth shortcut remains
  `/api/e2e/login`.
- Snake_case Blizzard wire keys do not appear in raider seed documents; the
  Blizzard reference-data fixture is allowed to preserve Blizzard wire names.
- Stale selector hits do not appear in specs. If `#instance-select` remains in
  a page object, confirm the app still renders that selector.
- Specs do not create untracked pages for behavior that needs diagnostics.
- Destructive tests use disposable seed identities.
