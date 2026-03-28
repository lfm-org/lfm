# Deep Code Quality Audit

- Date: 2026-03-28
- Scope: whole repository
- Mode: deep review
- Reviewer: Codex

## Summary

Overall assessment: adequate, not yet good.

The repo has strong orientation docs, strict TypeScript settings, a clear local verification contract, and mostly disciplined backend seams. It falls short of a `good` rating because `Testability` is weak and several high-sensitivity areas combine churn, shallow unit protection, and one confirmed browser regression.

Major strengths:

- strong repo-native documentation and architecture handoff
- fast local verification passes cleanly
- backend test surface is broad
- default browser suite is real and mostly stable

Major risks:

- CI does not enforce the repo's actual quality bar
- frontend unit tests are too shallow to protect complex stateful UI behavior
- one unhappy-path browser scenario is currently broken
- guild authorization still depends on a user-selected character too early in the trust chain

Verification limits after follow-up evidence collection:

- security scanner evidence is now available, but it is still less decisive than fixing the trust-boundary issue already found by manual review

## Evidence Summary

### Repo-native verification

- `./scripts/verify-local.sh fast`: passed
  - backend: 41 test files, 198 tests passed
  - frontend: 8 test files, 18 tests passed
  - frontend lint/build/bundle gate passed
  - infra alignment test passed

### Browser verification

- `bash scripts/e2e-all.sh`
  - default Playwright suite: 24 passed
  - `raids-empty`: passed
  - `raids-error`: passed
  - `characters-empty`: failed
- `bash scripts/e2e.sh instances-missing e2e/create-raid-instances-missing.spec.ts`
  - passed

The current known browser failure is:

- `frontend/e2e/characters-empty.spec.ts`

### Coverage verification

- `cd functions && npm run test:coverage`
  - passed
  - statements: `56.86%`
  - branches: `54.96%`
  - functions: `69.07%`
  - lines: `59.83%`
- `cd frontend && npm run test:coverage`
  - passed
  - statements: `74.57%`
  - branches: `54.34%`
  - functions: `75.75%`
  - lines: `77.35%`

Coverage reporting is now available natively in both packages after adding `@vitest/coverage-v8` and `test:coverage` scripts.

### Performance verification

- `env PLAYWRIGHT_INCLUDE_PERF_SPECS=1 bash scripts/e2e.sh default e2e/perf/async-actions.perf.spec.ts e2e/perf/forms.perf.spec.ts e2e/perf/load.perf.spec.ts e2e/perf/mobile.perf.spec.ts e2e/perf/navigation.perf.spec.ts`
  - 13 passed

The perf harness is therefore not just present on paper; it executed cleanly in this review.

### Complexity verification

- `lizard functions/src frontend/src`
  - 168 files analyzed
  - 1009 functions analyzed
  - average CCN: 2.0
  - warning functions: 9

Highest-risk hotspots identified by direct complexity tooling:

- `functions/src/functions/raids-update.ts` `applyRaidUpdate`: CCN 42
- `functions/src/functions/raids-signup.ts` `handler`: CCN 32
- `functions/src/functions/battlenet-character-portraits.ts` anonymous block: CCN 29
- `frontend/src/features/raids/components/RaidSignupCard.tsx` anonymous block: CCN 27
- `functions/src/functions/battlenet-callback.ts` `rejectWithClearedCookie`: CCN 24
- `frontend/src/features/raids/pages/CreateRaidPage.tsx` `validate`: CCN 24

### Security verification

- `semgrep scan --config=auto --json functions/src frontend/src`
  - 10 findings total
  - 4 `ERROR`, 6 `WARNING`
  - the 4 `ERROR` findings were generic secret detections in `functions/src/lib/battlenet.test.ts`
  - the remaining warnings were in `functions/src/lib/character-portrait.ts`, `functions/src/lib/test-mode.ts`, and `functions/src/scripts/e2e-test-data.ts`
- `trivy fs --scanners vuln,misconfig,secret --format json /src`
  - 0 dependency vulnerabilities reported
  - 0 secrets reported
  - 2 misconfigurations reported: 1 `HIGH`, 1 `LOW`

The scanner signal is useful but limited. The most important security concern in this repo is still the manually identified authorization boundary around selected characters.

## Metric Assessment

1. `Maintainability`: adequate
   - Clear repo structure and focused backend helpers, but a few large hotspot files still concentrate too much orchestration and fixture logic.
2. `Testability`: weak
   - The repo has a good local verifier and real browser tests, but CI does not enforce them, frontend unit tests are shallow, and one unhappy-path browser scenario currently fails.
3. `Reliability`: adequate
   - Fast verification passed and most browser flows passed, but the `characters-empty` scenario is a confirmed edge-path regression.
4. `Readability`: adequate
   - Most backend handlers are straightforward, but some React pages still mix fetch, URL state, responsive behavior, and rendering in one file.
5. `Documentation`: strong
   - README, architecture specs, security notes, and verification docs make the codebase easier to navigate than most repos of this size.
6. `Efficiency`: adequate
   - Bundle budgeting and perf harnesses exist, and the focused Playwright perf suite passed 13/13 in follow-up verification.
7. `Cyclomatic Complexity`: adequate
   - Direct tooling showed low average complexity overall, but a small set of raid and auth-adjacent hotspots still deserve refactoring attention.
8. `Technical Debt`: adequate
   - No obvious TODO/FIXME clusters, but test harness coupling and a handful of oversized files create maintenance drag.
9. `Extensibility`: adequate
   - Shared adapters and backend seams are reasonable, but complex UI pages still require invasive edits for small behavior changes.
10. `Code Security`: adequate
    - Semgrep and Trivy produced direct scan evidence with limited actionable findings, but the selected-character trust boundary remains a meaningful unresolved risk.
11. `Unit Test Results`: adequate
    - Repo-native tests passed cleanly, but the frontend unit layer is not exercising much real interaction logic.
12. `Code Churn`: weak
    - Recent churn overlaps auth, raid mutation, and test harness files, which are already sensitive areas.
13. `Code Coverage`: weak
    - Coverage is now measurable, but backend coverage remains shallow in several sensitive handlers including raid signup, auth, crypto, and selected-character flows.
14. `Reusability`: adequate
    - Shared adapters, sanitizers, and UI components reduce duplication without obvious harmful abstraction.
15. `Portability`: adequate
    - Runtime code is portable TypeScript, but local workflows assume bash, Docker, and Azure-oriented tooling.

## Top Findings

### 1. CI does not enforce the repo's real quality bar

- Metrics: `Testability`, `Reliability`
- Evidence:
  - `.github/workflows/ci.yml` only runs on `workflow_dispatch`
  - deploy workflows trigger on push to `main`
  - the strongest verifier lives in `scripts/verify-local.sh`, not in CI
- Why it matters for agentic development:
  - local confidence is high, but repository-wide protection is optional
  - repeated machine-assisted edits are only safe when the shared gate matches the local gate
- Likely consequence:
  - regressions that would be caught by local fast/browser checks can still reach `main`

### 2. The `characters-empty` browser scenario is broken

- Metrics: `Reliability`, `Testability`
- Evidence:
  - `frontend/e2e/characters-empty.spec.ts` failed during this review
  - `frontend/src/features/characters/pages/CharactersPage.tsx` falls back from `GET /battlenet/characters` to `POST /battlenet/characters/refresh`
  - `functions/src/scripts/e2e-test-data.ts` seeds an empty cached account profile for `characters-empty`
  - `functions/src/lib/test-mode.ts` still returns a non-empty default test-mode account profile on refresh
- Why it matters for agentic development:
  - the suite no longer protects a key unhappy path, so future edits can keep drifting away from expected onboarding behavior without a trustworthy failing signal
- Likely consequence:
  - broken first-time or empty-account flows can persist while the broader test suite still looks healthy

### 3. Guild authorization still depends on a caller-selected character too early

- Metrics: `Code Security`, `Reliability`
- Evidence:
  - `functions/src/functions/raider-character.ts` accepts `region` / `realm` / `name`, fetches the character profile, and persists it
  - guild-based access checks later trust the derived identity in raid read/create/signup flows
- Why it matters for agentic development:
  - auth and authorization bugs at trust boundaries have high blast radius and are easy to preserve accidentally when the invariants are implicit
- Likely consequence:
  - if Blizzard profile reads are possible for non-owned characters under this token scope, guild-scoped access can be derived from the wrong character

### 4. Frontend unit tests are too shallow for the complexity of the UI

- Metrics: `Testability`, `Readability`
- Evidence:
  - `frontend/vitest.config.ts` uses `environment: "node"`
  - representative tests render static markup and do not exercise interaction-heavy behavior
  - complex UI state lives in `CharactersPage.tsx`, `RaidsPage.tsx`, and `RaidSignupCard.tsx`
- Why it matters for agentic development:
  - fast local feedback is strongest when small UI changes can be validated without a full browser harness
- Likely consequence:
  - more regressions escape to slower Playwright runs, increasing iteration cost and reducing edit safety

### 5. A few large hotspot files still carry too much risk

- Metrics: `Maintainability`, `Code Churn`, `Extensibility`
- Evidence:
  - `functions/src/lib/test-mode.ts`
  - `functions/src/scripts/e2e-test-data.ts`
  - `frontend/src/features/raids/pages/RaidsPage.tsx`
  - `frontend/src/features/characters/pages/CharactersPage.tsx`
- Why it matters for agentic development:
  - large mixed-responsibility files are harder to reason about locally and increase blast radius for small edits
- Likely consequence:
  - changes in auth/test harness/UI orchestration remain slower and more failure-prone than they need to be

## Prioritized Remediation Plan

### `P0` Enforce the real verifier in CI

- Target metrics: `Testability`, `Reliability`
- Effort: medium
- Expected impact: high
- Suggested sequencing: first

### `P0` Fix the `characters-empty` contract and add focused regression protection

- Target metrics: `Reliability`, `Testability`
- Effort: small to medium
- Expected impact: high
- Suggested sequencing: alongside CI enforcement

### `P1` Enforce account ownership before persisting selected characters

- Target metrics: `Code Security`, `Reliability`, `Maintainability`
- Effort: medium
- Expected impact: high
- Suggested sequencing: after or alongside the failing scenario fix

### `P1` Strengthen the frontend interaction test layer

- Target metrics: `Testability`, `Reliability`, `Readability`
- Effort: medium
- Expected impact: medium to high
- Suggested sequencing: after the `characters-empty` fix stabilizes the current browser contract

### `P2` Split remaining hotspot files with mixed responsibilities

- Target metrics: `Maintainability`, `Extensibility`, `Code Churn`
- Effort: medium
- Expected impact: medium
- Suggested sequencing: after the highest-risk correctness issues are addressed

## Residual Unknowns

No remaining evidence-gathering unknowns from the initial audit scope.

The remaining work is remediation rather than measurement:

- enforce the verifier in CI
- fix the broken `characters-empty` browser contract
- harden the selected-character authorization boundary
- improve frontend interaction tests and backend coverage in the weakest handlers
