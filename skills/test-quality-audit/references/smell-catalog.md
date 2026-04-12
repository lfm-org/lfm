# Smell Catalog (core, framework-neutral)

Compact reference for cite-by-code usage in audit reports. Codes align 1:1 with [../../../docs/quality-reference/unit-testing.md §5](../../../docs/quality-reference/unit-testing.md) (unit rubric), [../../../docs/quality-reference/integration-testing.md §5](../../../docs/quality-reference/integration-testing.md) (integration rubric), and [../../../docs/quality-reference/e2e-testing.md §5](../../../docs/quality-reference/e2e-testing.md) (E2E rubric). The reference docs are the source of truth — this file exists so audit findings can cite codes like `HC-2`, `LC-3`, `I-HC-A4`, `I-LC-2`, `E-HC-F3`, `E-POS-5` instead of restating each smell.

Framework-specific smells are namespaced in their extension file (e.g. `dotnet.HC-1`). Each extension smell may declare an `Applies to:` field of `unit`, `integration`, `e2e`, or any combination; absent tag defaults to `unit` for backwards compatibility.

## Unit rubric

Applies when `SKILL.md` step 0b selects the unit rubric. Cite as `HC-N`, `LC-N`, `POS-N`.

### High-confidence smells (HC-*)

`HC-1` — No assertions; body calls SUT but asserts nothing (or only "no exception thrown").
`HC-2` — Tautological assertion; expected value is computed by duplicating SUT logic in the test.
`HC-3` — Pasted-literal assertion; oddly specific expected value with no spec, fixture, or comment provenance.
`HC-4` — Logic in the test; `if`, `for`, `while`, try/catch with conditional assertion.
`HC-5` — Mock-return-then-mock-called-with; test sets up a mock to return X then asserts the mock was called with Y derived from X.
`HC-6` — Over-specified interaction assertions; `verify(..., times(N))` where count/args reflect loop structure, not observable outcome.
`HC-7` — Name describes HOW, not WHAT; e.g. `Calls_Repository_Save_With_Entity` vs `Persists_Order_When_Checkout_Succeeds`.
`HC-8` — Test depends on execution order; static mutable state, shared fixtures without reset, alphabetical ordering reliance.
`HC-9` — Disabled or stubbed-out assertions; `assertTrue(true)`, commented-out asserts, `skip()` with no linked issue.
`HC-10` — Snapshot tests pinning unspecified output; `toMatchSnapshot()` on rendered output with no accompanying spec.
`HC-11` — Mocks of clock/fs/network returning hardcoded "real" values; signals the test was calibrated against a specific run.

### Low-confidence smells (LC-*)

`LC-1` — Heavy mocking of same-layer code; mocking classes owned by the same module as the SUT.
`LC-2` — Assertions only on structural shape; `hasProperty('id')` without asserting what the id should be.
`LC-3` — Fixture passed through unchanged and asserted equal to the fixture; possible valid identity, possible tautology.
`LC-4` — One test per public method, all named `MethodName_Works`; ceremony over behavior thinking.
`LC-5` — Dates, GUIDs, or random values in the expected position; usually masks non-determinism.
`LC-6` — Zero negative tests for a function with documented error modes.
`LC-7` — Excessive setup (>20 lines) for a single assertion; either too-many-dependencies SUT or test reconstructs production state instead of isolating behavior.
`LC-8` — Parameterized test where all cases assert the same thing; parameterization isn't doing work.

### Positive signals (POS-*)

`POS-1` — Test name reads as a requirement sentence.
`POS-2` — Expected value has an external source (spec, RFC vector, sample file, domain invariant, linked ticket).
`POS-3` — Assertions on return value or published side effect (not on internal mock invocations).
`POS-4` — Parameterized tests covering boundaries/equivalence classes with *varied* expected values.
`POS-5` — Separate tests for happy path and each distinct sad path.
`POS-6` — Comments citing a requirement, spec, or invariant on non-obvious expected values.
`POS-7` — Test expresses an invariant (round-trip, idempotency, commutativity, associativity, bounds).

## Integration rubric

Applies when `SKILL.md` step 0b selects the integration rubric. Codes are prefixed `I-` to distinguish from unit-rubric codes above. Sub-lane A is in-process; sub-lane B is out-of-process contract. See [../../../docs/quality-reference/integration-testing.md §5](../../../docs/quality-reference/integration-testing.md) for the full rationale.

### High-confidence smells, sub-lane A / in-process (I-HC-A*)

`I-HC-A1` — Every dependency mocked; no real seam exercised. Belongs in the unit lane.
`I-HC-A2` — Shared seed data mutated across tests; cross-test pollution by construction.
`I-HC-A3` — Test depends on migration ordering without declaring it.
`I-HC-A4` — Shared container with no per-test data scoping; first test's writes are the second test's preconditions.
`I-HC-A5` — `Thread.Sleep`, `WaitFor`, retry-until-green loops; flake in slow motion.
`I-HC-A6` — Test asserts on log text that is not a published audit event.
`I-HC-A7` — Migration test runs against an empty database and asserts nothing about row state.
`I-HC-A8` — Snapshot of a full entity graph with no schema source; characterization at the integration layer.
`I-HC-A9` — Test writes data and never cleans up, or only cleans up on the happy path.
`I-HC-A10` — Incidental coverage; test sweeps a large amount of code into execution but the only assertion is "no exception thrown."
`I-HC-A11` — Fragile setup; arrange block depends on state it cannot guarantee (its own retry loop, seed data it did not create, live third-party boundary without a stub, shared mutating factory) — non-deterministic by construction.

### High-confidence smells, sub-lane B / out-of-process contract (I-HC-B*)

`I-HC-B1` — Assertion on an implementation-detail response field with no spec reference.
`I-HC-B2` — Test hits a test-only endpoint that does not exist in production.
`I-HC-B3` — Snapshot of a full response body with no OpenAPI / JSON Schema / Protobuf source.
`I-HC-B4` — Hardcoded port, container name, hostname, or environment URL.
`I-HC-B5` — Downstream service mocked at the transport layer; defeats the sub-lane, belongs in unit lane or as a contract test.
`I-HC-B6` — Retry test stubs the transport; the SUT's retry code path is never really executed.
`I-HC-B7` — Auth test with only a happy-path valid token; no negative cases.
`I-HC-B8` — Contract test whose "expected" payload was pasted from a recorded run with no consumer behind it.

### Low-confidence smells, shared across sub-lanes (I-LC-*)

`I-LC-1` — One giant test class covering unrelated features.
`I-LC-2` — Test name ends in `_Works` / `_Integration` / `_EndToEnd` with no requirement in the name.
`I-LC-3` — Expected values with no external provenance; may be fine, may be pasted from the SUT.
`I-LC-4` — Parameterized test where every case asserts the same thing.
`I-LC-5` — Fixture rebuilt from scratch in every test; may indicate the fixture is too ambitious.
`I-LC-6` — Broad integration test where a narrow one would do; seam conflation.

### Positive signals (I-POS-*)

`I-POS-1` — Test name reads as a requirement sentence and names the seam being exercised.
`I-POS-2` — Expected value has external provenance (spec, OpenAPI, JSON Schema, RFC, consumer pact, ticket, domain invariant).
`I-POS-3` — Narrow by default; one seam per test. Broad tests carry a comment explaining why.
`I-POS-4` — Per-test data ownership; factory plus cleanup, no shared mutable fixture.
`I-POS-5` — Hermetic by construction; runs offline, runs in parallel with itself, produces the same result twice in a row.
`I-POS-6` — Asserts on a published contract (status code, error envelope shape, audit event schema, OpenAPI fragment) with a cited source.
`I-POS-7` — Test expresses an invariant (round-trip, idempotency, "publish only after commit") rather than a single point.

## E2E rubric

Applies when `SKILL.md` step 0b selects the E2E rubric. Codes are prefixed `E-` to distinguish from unit-rubric (`HC-*` / `LC-*` / `POS-*`) and integration-rubric (`I-HC-*` / `I-LC-*` / `I-POS-*`) codes. High-confidence smells are split by sub-lane because the failure modes are incompatible; low-confidence smells and positive signals are shared. See [../../../docs/quality-reference/e2e-testing.md §5](../../../docs/quality-reference/e2e-testing.md) for the full rationale.

Sub-lanes: **F** functional user journey, **A** accessibility audit, **P** performance budget, **S** security surface.

### High-confidence smells, sub-lane F / functional journey (E-HC-F*)

`E-HC-F1` — No user-observable assertion; clicks through a flow but asserts only a URL match or element presence.
`E-HC-F2` — Implementation selectors (CSS class, xpath, internal id, test-id without accessible-name fallback) instead of role/label/accessible-name locators.
`E-HC-F3` — Hardcoded wall-clock waits: sleeps, fixed-duration delays, fixed-budget wait primitives, retry-until-green loops.
`E-HC-F4` — Happy path only; one valid user, one valid input, no error state, no edge case.
`E-HC-F5` — Shared browser context, cookies, localStorage, or sessionStorage across tests; cross-test pollution by construction.
`E-HC-F6` — Test name describes UI steps (`Clicks_Login_Button`) instead of user outcomes (`Anonymous_User_Signs_In`).
`E-HC-F7` — DOM snapshot / golden-file assertion against rendered markup with no published schema; characterization at the most expensive layer in the suite.
`E-HC-F8` — Pixel / visual regression against a baseline with no review gate, owner, or drift process.
`E-HC-F9` — Asserts against third-party widget internals the test does not own (OAuth provider DOM, payment iframe, analytics SDK).
`E-HC-F10` — Test exercises only what an integration test already proves (HTTP contract shape, status codes, response body); belongs in the integration lane.
`E-HC-F11` — Fragile setup; per-test arrange block depends on state it cannot guarantee (retry loop in setup, seed data not created by the test, UI-navigation to create preconditions a backend API could seed, live third-party boundary, shared mutating factory).

### High-confidence smells, sub-lane A / accessibility audit (E-HC-A*)

`E-HC-A1` — Axe rule suppressions with no linked justification; silently pins current violations.
`E-HC-A2` — Scans only at page load; dynamic content, modals, menus, focus traps never audited.
`E-HC-A3` — No keyboard-flow assertions (tab order, focus indicator, focus return, Escape behavior, Enter/Space activation).
`E-HC-A4` — Asserts total violation count rather than violation IDs; a new violation can hide a fix for an old one.
`E-HC-A5` — No WCAG conformance level cited; "passes axe" without declaring the target (2.1 AA, 2.2 AA, etc.) is not a contract.
`E-HC-A6` — Axe scoped to a subtree that excludes out-of-tree content (portals, tooltips, modals mounted to `document.body`); the scope is lying about what is actually on the page.

### High-confidence smells, sub-lane P / performance budget (E-HC-P*)

`E-HC-P1` — Perf budget with no external source (Web Vitals threshold, RUM baseline, team SLO); a pasted literal in performance clothing.
`E-HC-P2` — Single-sample assertion on a noisy metric (one LCP measurement, not p75/p95 across N runs with N and the percentile declared).
`E-HC-P3` — No cold-vs-warm distinction; a cached navigation is asserted at the same budget as a cold load.
`E-HC-P4` — Budget asserted against localhost; proves nothing about production perf.
`E-HC-P5` — Perf test quarantined with a skip tag instead of having its flake root-caused.
`E-HC-P6` — Hardware or CI-shape assumption uncontrolled (CPU count, GPU, throttling profile, container CPU share); the test measures the hardware, not the code.

### High-confidence smells, sub-lane S / security surface (E-HC-S*)

`E-HC-S1` — Asserts a security header's value only; does not exercise the browser's enforcement of it. If a browser is not needed to prove the contract, move to integration sub-lane B.
`E-HC-S2` — XSS or injection payload test with one pasted-literal payload and no reference to OWASP, a payload list, or a fuzzing corpus.
`E-HC-S3` — Auth test with only a happy-path valid session; no expired token, missing token, tampered cookie, or cross-user access attempt.
`E-HC-S4` — CSP / CORS / frame-ancestors test asserts server response without verifying browser-side blocking; the contract under test is browser behavior, not the header.
`E-HC-S5` — Security test runs against a test-only endpoint, debug route, or build flavor that does not exist in production.

### Low-confidence smells, shared across sub-lanes (E-LC-*)

`E-LC-1` — One giant test class covering journey, a11y, perf, and security concerns at once.
`E-LC-2` — Test name ends in `_Works` / `_E2E` / `_Journey` / `_Integration` with no user outcome in the name.
`E-LC-3` — Parameterized test where every case asserts the same thing.
`E-LC-4` — Full stack launched for a one-line DOM-text assertion; may be critical smoke, may be incidental coverage.
`E-LC-5` — Page Object with >20 methods covering multiple unrelated pages; likely a god-object carrying multiple lanes' concerns.
`E-LC-6` — Test's only wait is a navigation predicate with no subsequent content check; navigation completed but page state is unverified.

### Positive signals (E-POS-*)

`E-POS-1` — Test name reads as a user story (`Anonymous_User_Signs_In_And_Reaches_Authenticated_Home`).
`E-POS-2` — Locators derived from accessible role, accessible name, visible text, label association, or placeholder; no raw CSS or xpath in the test body.
`E-POS-3` — Condition-based waits only; no wall-clock sleeps, fixed-duration delays, or fixed-budget wait primitives anywhere in the test or its helpers.
`E-POS-4` — Perf budget cited against Web Vitals / RUM baseline / SLO with a link in the test or a peer comment.
`E-POS-5` — A11y audit cites a WCAG conformance level, runs after interaction as well as at load, and any rule suppression has a linked justification.
`E-POS-6` — Per-test data, user, and session ownership; each test creates its own fixtures and owns its own browser context.
`E-POS-7` — Traces, screenshots, DOM snapshots, console logs captured on failure *as diagnostics* — not consumed by any assertion.
`E-POS-8` — Hermetic stack bringup (containerized adjacent dependencies, dynamic ports, readiness waits, deterministic seed data).
`E-POS-9` — Security test exercises a browser-specific enforcement path (CSP blocks an injected script, cookie jar respects `SameSite`, cross-origin iframe blocked) rather than asserting a header value.
