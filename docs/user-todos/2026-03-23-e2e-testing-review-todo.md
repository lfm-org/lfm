# E2E Testing Review Todo

## Current state

- Playwright coverage lives under `frontend/e2e/`.
- The main local runner is `./scripts/dev-env.mjs test`, and `./scripts/e2e.sh ...` is the compatibility wrapper.
- Default Playwright discovery currently lists 16 tests in 9 files.
- Scenario-specific specs are intentionally excluded from default discovery and are meant to be run separately with the correct seed state.
- CI currently runs `./scripts/e2e.sh`, not a true all-scenarios command.

## Findings

### 1. The e2e harness is currently blocked before Playwright starts

Running `./scripts/e2e.sh access-control` currently fails during `load-test-reference-data.js`, before the browser suite starts. The concrete failure is:

`Bearer token authentication is not permitted for non-TLS protected (non-https) URLs.`

This points to local blob storage setup against Azurite still using bearer-token auth over HTTP. Cosmos already has explicit local HTTP handling, but blob access still needs the equivalent local-test path.

### 2. Default local and CI execution miss scenario-specific coverage

The default Playwright config excludes:

- `raids-empty.spec.ts`
- `raids-error.spec.ts`
- `characters-empty.spec.ts`
- `create-raid-instances-missing.spec.ts`

That split is reasonable, but it means `./scripts/e2e.sh` and the current CI job do not represent full e2e coverage.

### 3. `scripts/e2e-all.sh` is closer to the real suite, but still not complete

`scripts/e2e-all.sh` explicitly runs the scenario-specific specs with the correct seed states, but it still omits at least `frontend/e2e/raids-localized-names.spec.ts`.

There is currently no single documented command that clearly means "run every e2e spec we care about."

### 4. Agent-facing e2e guidance is stale

The repo still contains older e2e design and plan docs that describe previous architectures. They are useful as history, but they no longer describe the live stack closely enough to be reliable day-to-day guidance for agents.

## User todos

- Fix local blob auth for Azurite so `./scripts/e2e.sh ...` can reach Playwright instead of failing during reference-data loading.
- Define and document one canonical "full e2e suite" command.
- Update CI to run that canonical full-suite command instead of only the default scenario run.
- Decide whether `scripts/e2e-all.sh` should become the canonical command or whether the default runner should absorb scenario coverage directly.
- Add `frontend/e2e/raids-localized-names.spec.ts` to the intended full-suite path.
- Review old e2e design/plan docs and either update them, clearly mark them historical, or add a newer canonical guidance document.

## Proposed `CLAUDE.md` chapter: E2E Testing

The section below is intended as future agent guidance, not as an implemented `CLAUDE.md` change in this task.

### E2E Testing

Use Playwright e2e coverage when a change affects:

- login, logout, callback, or protected-route behavior
- flows that depend on `TEST_MODE` auth or seeded e2e data
- multi-step user journeys such as create, update, cancel, or redirect flows
- public landing, login, or other high-visibility entry pages
- accessibility-critical interaction paths
- regressions that were not well protected by unit or type-level checks

Before changing e2e tests:

- run `npm --prefix frontend run e2e:list` to see current default discovery
- remember that default discovery excludes scenario-specific specs from `frontend/playwright.config.ts`
- inspect `scripts/e2e-all.sh` before assuming the default runner covers the whole suite

When adding or updating e2e coverage:

- prefer a focused run first, for example `./scripts/e2e.sh signup`
- if the spec needs non-default seed data, run it with the correct scenario instead of forcing it into the default suite
- when adding a new scenario-only spec, update both `frontend/playwright.config.ts` and `scripts/e2e-all.sh`
- keep tests deterministic and based on local `TEST_MODE` fixtures and seed data
- do not introduce real Battle.net dependencies into routine e2e coverage

When claiming e2e verification:

- do not say "full e2e suite passed" unless the command used actually covers all intended specs
- distinguish between:
  - a focused spec run
  - the default e2e run
  - the intended all-scenarios run
- if the harness fails before Playwright starts, report that as an infrastructure blocker rather than a passing or failing browser result
