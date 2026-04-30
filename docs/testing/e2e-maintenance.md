# E2E Maintenance

E2E tests are reserved for behavior that only a real browser and full local
stack can prove. Prefer unit, API, app-core, or bUnit tests when those lanes can
prove the same contract.

## Lanes

| Lane | Use E2E for | Move down when |
|------|-------------|----------------|
| Functional | Multi-step user journeys across browser, SPA, API, storage, and redirects | A single component, DTO, API status, or selector proves the behavior |
| Accessibility | axe scans and keyboard/focus states that need browser rendering | Static labels or component markup can be checked in bUnit |
| Security | Browser-enforced CORS, CSP, iframe, cookie, and redirect behavior | Server-only status/header logic can be tested in API tests |
| Auth flow | Real redirect/callback/session behavior | `/api/e2e/login` is enough and the browser adds no signal |

## Required Test Comment

Every new E2E test must state:

1. The user-observable outcome.
2. Why a cheaper test cannot prove it.
3. Whether it mutates shared data.

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

## Drift Audit

Run these checks after UI, API route, or storage-shape changes:

```bash
rg -n "api/battlenet/|wow_accounts|playable_class|modekey-input|#instance-select" tests/Lfm.E2E app api
rg -n "AuthenticatedContextAsync|NewPageAsync\\(" tests/Lfm.E2E/Specs
rg -n "DeleteAsync|DELETE|me-delete|DeleteAccount" tests/Lfm.E2E/Specs tests/Lfm.E2E/Seeds
```

Expected:

- Legacy routes only appear in production wire-model comments or API XML comments.
- Snake_case Blizzard wire keys do not appear in E2E Cosmos seed documents.
- Specs do not create untracked pages for behavior that needs diagnostics.
- Destructive tests use disposable seed identities.
