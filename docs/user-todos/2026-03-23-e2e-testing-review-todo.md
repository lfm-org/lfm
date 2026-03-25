# E2E Testing Review Status

## Current repo state

- Playwright coverage lives under `frontend/e2e/`.
- `./scripts/dev-env.mjs test` is the primary focused runner.
- `./scripts/e2e.sh` remains the compatibility wrapper for scenario-targeted and focused runs.
- `./scripts/e2e-all.sh` is the canonical full-suite command and is the path used by CI.
- Default Playwright discovery runs with `workers: 1` because the local Docker-backed seed state is shared across specs.
- Default Playwright discovery intentionally excludes scenario-specific specs listed in [frontend/playwright.config.ts](/home/souroldgeezer/repos/sisu-raidcal/frontend/playwright.config.ts).

## Notes

- The earlier Azurite HTTP auth blocker described in this doc is no longer current for the repo state this file now tracks.
- `frontend/e2e/raids-localized-names.spec.ts` participates through default discovery and therefore is part of `./scripts/e2e-all.sh`.
- When adding a new scenario-only spec, update all three integration points:
  - [scripts/dev-env.mjs](/home/souroldgeezer/repos/sisu-raidcal/scripts/dev-env.mjs)
  - [frontend/playwright.config.ts](/home/souroldgeezer/repos/sisu-raidcal/frontend/playwright.config.ts)
  - [scripts/e2e-all.sh](/home/souroldgeezer/repos/sisu-raidcal/scripts/e2e-all.sh)

## Operator guidance

- Use `npm --prefix frontend run e2e:list` to inspect default discovery.
- Use `./scripts/dev-env.mjs test <spec>` for focused default-scenario verification.
- Use `./scripts/e2e.sh <scenario> <spec>` when the spec depends on non-default seeded data.
- Use `./scripts/e2e-all.sh` before claiming full browser coverage.
