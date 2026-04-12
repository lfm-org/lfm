# End-to-End Test Quality — Reference

A language- and framework-agnostic synthesis of authoritative guidance on end-to-end (browser-driven) test quality. Sibling to [unit-testing.md](unit-testing.md) and [integration-testing.md](integration-testing.md). Written to be directly usable as the rubric and reasoning substrate for a test-quality audit agent.

## Context

E2E tests inherit both failure modes of the lanes below them, amplified.

The unit rubric fails at **intent vs. echo** (see [unit-testing.md §1](unit-testing.md)). The integration rubric fails at **defensible scope** — the "what is this integrating, and why couldn't a unit test prove the same thing?" question (see [integration-testing.md §1](integration-testing.md)). E2E tests fail at **both**, stacked:

1. They do not test anything distinctively end-to-end. A test that could have been a unit or integration test but uses a real browser for ceremony is pure cost.
2. They are recordings of what the UI happened to do when the test was written, not specifications of what the user needs to accomplish. DOM snapshots and pixel diffs are characterization by another name, moved up the stack.

Both modes compound. An E2E test is the slowest, flakiest, most expensive test in any suite, and it has the largest surface for characterization (every pixel, every DOM node, every timing measurement). The rubric is therefore stricter than the lanes below it: an E2E test must earn its place twice — once by exercising something no cheaper test can, and once by deriving from user intent rather than observed UI state.

This document covers browser-driven tests executed against a deployed or locally-launched stack through a real user agent (Playwright, Cypress, Selenium, WebDriverIO, or equivalent). It treats the browser as the primary seam. Out of scope: API-level "end-to-end" tests that do not drive a browser — those are sub-lane B of the integration rubric.

This document does not analyze any specific codebase. It states principles, smells, and rubrics.

---

## 1. The central problem: scope and provenance, both

E2E tests have two operational questions. Every test must have a defensible answer to **both**.

> *(scope)* What user-observable outcome does this test prove, and why couldn't a cheaper test prove the same thing?
>
> *(provenance)* Could this test have been written from the user story alone, without observing what the UI currently does?

A test that fails the scope question is slower and flakier than whatever lane it belongs in. Move it down.

A test that fails the provenance question is pinning current UI output — a screenshot of today's app wearing a test costume. When the UI refactors, the test will either break (and be deleted as "noise") or be updated to pin the new output (and continue proving nothing). Neither case catches a regression the test was meant to prevent.

The rubric is deliberately strict on both axes because E2E is where test-suite debt compounds fastest. A flaky characterization test at the unit layer costs a retry; the same test at the E2E layer costs a full stack bringup and a quarantine decision.

### Vocabulary used throughout

- **User journey** — a sequence of user-observable steps that together accomplish a stated task. The unit of specification for sub-lane F.
- **Accessible-name selector** — a locator derived from what a user (or assistive technology) would perceive: role, label, placeholder, visible text. The opposite of an implementation selector (CSS class, xpath, internal id).
- **Condition-based wait** — a wait that resolves when an observable state becomes true: an element is visible, a URL matches, a network response arrives, a predicate holds. The opposite of a wall-clock wait, which resolves when a fixed interval has elapsed regardless of page state.
- **Hermetic** — the test brings every piece of state it needs with it, depends on nothing external, and produces the same result on every run. Same definition as in [integration-testing.md §1](integration-testing.md); doubly load-bearing at the E2E layer because the browser adds state surfaces the integration layer does not have (cookies, localStorage, sessionStorage, indexedDB, service workers).
- **Cold vs. warm load** — whether the navigation under test is a first visit (no cache, no service worker, no HTTP/2 push warm-up) or a repeat visit. Perf budgets that ignore the distinction are comparing noise.
- **Web Vitals** — the published Google metric set for page performance: LCP (Largest Contentful Paint), INP (Interaction to Next Paint), CLS (Cumulative Layout Shift). The canonical external source for perf-budget provenance in sub-lane P.
- **WCAG conformance level** — the published W3C accessibility conformance target (A, AA, AAA for WCAG 2.1 or 2.2). The canonical external source for a11y provenance in sub-lane A.

---

## 2. Consensus characteristics of a quality E2E test

Synthesized across Fowler (*TestPyramid*, *BroadStackTest*), Cohn (Test Pyramid), Kent C. Dodds (Test Trophy), the Google testing book and Testing Blog, Playwright / Cypress / WebDriverIO best-practice docs, Deque / axe-core, Web Vitals, WCAG 2.2, and the OWASP ASVS.

### 2.1 Proves a user-observable outcome
The assertion is something a user can verify: a page reached, a piece of content visible, a form submitted, a download triggered, a session established. "No exception thrown" and "URL matches" are the lowest possible bar and almost always insufficient on their own.

### 2.2 Derived from a user story, journey, or acceptance criterion
The test name and body should read as a sentence a product owner would recognize. If the name describes *clicks and keystrokes*, the test is pinning the UI. If it describes *a task a user is trying to accomplish*, the test is specifying a journey.

### 2.3 Uses accessible-name selectors
A locator named by accessible role plus accessible name ("the button labelled *Sign in*") is specification. A locator that looks up a dedicated `data-testid` attribute is a bridge — acceptable when no accessible name is available, but it encodes no user-facing meaning. A deep CSS or xpath path (`.sidebar > ul > li:nth-child(3) a`) is characterization: it will break on every layout refactor and is indistinguishable from a DOM snapshot. Accessible-name selectors double as an implicit accessibility smoke test: if the selector cannot find the element, neither can a screen reader.

### 2.4 Waits by condition, never by wall clock
Fixed-duration sleeps and retry-until-green loops are Beck's *SlowTest* plus Khorikov's *flake in slow motion* (see [integration-testing.md §5.1](integration-testing.md)). At the E2E layer they are doubly disqualifying because the browser exposes real state predicates — element visibility, URL match, network response, DOM-condition polling with a condition rather than a clock. If the framework cannot express the condition the test is waiting for, that itself is a finding.

### 2.5 Hermetic, with per-test data and session ownership
Every test owns its own user account, its own browser context, its own cookies and storage. No test reads state another test wrote. No test inherits a session or a logged-in state from a sibling. Cleanup runs on failure as well as on success. See [integration-testing.md §7](integration-testing.md) for the shared ruleset; the E2E layer adds *browser state* to the list of surfaces that must be reset.

### 2.6 Diagnosable on failure
Traces, screenshots, DOM snapshots, console logs, and network logs are captured on failure — **as diagnostics, never as assertion substrate**. The distinction is load-bearing: a screenshot saved on failure is a debugging artifact; a screenshot compared against a baseline *is* the assertion, and that assertion has the provenance problem of §1 unless there is a baseline workflow with an owner and a review gate.

### 2.7 Runs few, runs deterministically
The Google testing book's guidance — *"just say no to more end-to-end tests"* — is the floor, not the ceiling. E2E test count should be deliberately small, each test should cover a high-value journey, and the suite should be deterministic enough to run on every commit without a retry budget. A suite that grows linearly with feature count is a suite that will be quarantined within two quarters.

### 2.8 Trustworthy, readable, maintainable
Osherove's three pillars (see [unit-testing.md §2.6](unit-testing.md)). An E2E test that is not trusted gets quarantined within a sprint; an E2E test that nobody can read gets deleted in the next refactor. Both failures remove coverage without visible cost — the dashboards still show a green suite. Treat trust and readability as load-bearing, not cosmetic.

---

## 3. What to test — decision heuristics

The four sub-lanes have different jobs. Each earns its place differently.

### 3.1 Sub-lane F — functional user journey

| Heuristic | Notes |
|---|---|
| High-value user journeys that cross layers no cheaper test exercises end-to-end | Sign-in, checkout, onboarding, primary feature flows |
| Auth and session state | Cookies, CSRF, session rotation, concurrent tabs |
| SPA route transitions and history | Deep links, back/forward, refresh on a protected route |
| Conditional UI that depends on server state | Feature flags, entitlements, empty states, paginated lists |
| Error recovery | Network failure mid-flow, 4xx/5xx surfaced to the user |

### 3.2 Sub-lane A — accessibility audit

| Heuristic | Notes |
|---|---|
| Axe (or equivalent) scan against a declared WCAG conformance level | Scope the scan; cite the level. "Passes axe" without a level is not a contract. |
| Re-scan after interaction | Dynamic content, modals, drawers, menus, focus-trap regions |
| Keyboard flow | Tab order, focus indicator, focus return on close, Enter/Space activation, Escape on modal |
| Announcements for dynamic updates | `aria-live` regions, toast notifications, loading states |
| Viewport and zoom variants where relevant | Mobile layout, 200% zoom, reduced-motion preference |

### 3.3 Sub-lane P — performance budget

| Heuristic | Notes |
|---|---|
| Web Vitals on representative pages | LCP, INP, CLS. Cite the threshold source — Google's published values or a team SLO. |
| Critical-path timings under cold load | TTFB, DCL, first-byte of critical API, first meaningful paint |
| SPA route-transition timings | Distinct from cold load; controlled for warm cache |
| API call count, size, and duration budgets on key flows | Regressions where a refactor adds a request the old UI did not need |
| Resource budgets | Bundle size, image payload, total transferred bytes |

### 3.4 Sub-lane S — security surface

| Heuristic | Notes |
|---|---|
| Browser enforcement of declared policies | CSP blocks an injected `<script>`, X-Frame-Options blocks a cross-origin iframe, CORS blocks a cross-origin fetch |
| Cookie jar attributes under real navigation | `HttpOnly`, `Secure`, `SameSite` actually survive in the browser, not just in the response header |
| Session lifecycle | Invalidation on logout, fixation resistance, rotation on privilege change, expired-cookie behavior |
| Cross-user access paths | Authenticated user A cannot reach user B's resources through routed navigation |
| Negative-space auth | Tampered cookie, expired token, missing token, malformed token, insufficient scope |

### 3.5 Narrow vs. broad

E2E tests are broad by construction — the whole stack is live. "Narrow" at this layer means *exactly one user outcome per test*, not one dependency. A test that proves sign-in *and* profile edit *and* sign-out in a single method is broad; split into three.

---

## 4. What not to test (or test lightly)

- **Anything the unit or integration lanes already prove.** Business logic, input validation, HTTP contract shape, serialization, routing, auth-token parsing. Promoting these to E2E buys nothing but flake.
- **Implementation details behind the UI.** Internal state stores, component props, event handler names, JS module structure. Test what the user sees, not what the framework does.
- **CSS for aesthetic preference.** Alignment, margin, color, font — unless they are accessibility-load-bearing (contrast ratios, focus indicator visibility) and cited against WCAG. A pixel-perfect test with no owner is characterization at the highest cost tier in the suite.
- **Third-party widgets at their internal level.** OAuth provider pages, payment iframes, analytics SDKs, embedded maps. You do not own the DOM under test and cannot control when it changes.
- **Cloud-platform behavior as a product.** CDN caching, load balancer behavior, DNS — the platform is not under test; your configuration of it is, and configuration belongs in integration sub-lane B.
- **Snapshot of rendered output without a published schema.** A pinned HTML blob or JSON response with no OpenAPI, JSON Schema, or contract doc is characterization amplified by browser cost. See `E-HC-F7` below and [integration-testing.md §5.1.8](integration-testing.md) for the integration-layer analogue.
- **Pixel comparisons without a baseline workflow.** Visual regression is a legitimate tool *only* when there is a review gate for baseline updates, an owner for design drift, and a story for handling cross-platform rendering differences. Without that, it is a screenshot glued to an assertion.

---

## 5. Detectable signals

Codes are prefixed `E-` to distinguish from unit (`HC-*` / `LC-*` / `POS-*`) and integration (`I-HC-A*` / `I-HC-B*` / `I-LC-*` / `I-POS-*`).

High-confidence smells are split by sub-lane because the failure modes are incompatible. Low-confidence smells and positive signals are shared.

### 5.1 High-confidence smells, sub-lane F (functional journey) — `E-HC-F1..E-HC-F10`

1. **`E-HC-F1`** — No user-observable assertion. The test clicks through a flow and asserts only a URL match or an element's presence. The journey could have silently failed in the middle and the test would still pass.
2. **`E-HC-F2`** — Implementation selectors: CSS class, xpath, internal id, test-id without accessible-name fallback. Breaks on any layout refactor, catches no user-visible regression.
3. **`E-HC-F3`** — Hardcoded wall-clock waits: sleeps, fixed-duration delays, fixed-budget wait primitives, retry-until-green loops. Flake by construction. (Analogue of [`I-HC-A5`](integration-testing.md).)
4. **`E-HC-F4`** — Happy path only. One valid user, one valid input, no error state, no edge case. The negative space is where UI bugs live — and E2E is often the only layer that can see them.
5. **`E-HC-F5`** — Shared browser context, cookies, localStorage, or sessionStorage across tests. Cross-test pollution by construction. (Browser-layer analogue of [`I-HC-A2`](integration-testing.md).)
6. **`E-HC-F6`** — Test name describes UI steps (`Clicks_Login_Button_Then_Types_Email`) instead of user outcomes (`Anonymous_User_Signs_In`). The name is the first thing the audit checks against the provenance question in §1.
7. **`E-HC-F7`** — DOM snapshot with no published schema: a saved-HTML diff, a serialized component tree, or any golden-file assertion against rendered markup with no spec to anchor it. Characterization at the most expensive layer in the suite.
8. **`E-HC-F8`** — Pixel / visual regression against a baseline with no review gate, no owner, and no drift process. The baseline is a photograph of today's build.
9. **`E-HC-F9`** — Asserts against third-party widget internals the test does not own — OAuth provider DOM, payment iframe, analytics SDK state. The widget will change on a schedule the test cannot control.
10. **`E-HC-F10`** — Test exercises only what an integration test already proves: HTTP contract, status codes, response body shape, header values. Move to sub-lane B of the integration rubric.
11. **`E-HC-F11`** — Fragile setup. The per-test arrange block depends on state or behavior it cannot guarantee: its own retry loop, seed data the test did not create, UI-navigation to create preconditions a backend API could seed more reliably, a live third-party boundary with no stub (real OAuth, payment, email delivery), or a shared factory that mutates across tests. Distinct from `E-HC-F3` (wall-clock waits in the *test body*) and `E-HC-F5` (shared *browser context* across tests): this smell targets the *arrange block itself* as a risk surface. A setup block with its own retry or wait budget is the strongest signal — the author knew the setup was fragile and papered over it. Rewrite: seed preconditions via a backend API rather than the UI; use per-test factories with unique keys (`E-POS-6`); stub third-party boundaries at the deployment boundary.

### 5.2 High-confidence smells, sub-lane A (accessibility audit) — `E-HC-A1..E-HC-A6`

1. **`E-HC-A1`** — Axe rule suppressions with no linked justification (ticket, spec exemption, vendor-widget limitation with a fix date). Silently pins current violations; new regressions in suppressed rules are never caught.
2. **`E-HC-A2`** — Scans only at page load. Dynamic content, modals, menus, drawers, and focus traps are never audited. Most real a11y bugs live after the first interaction.
3. **`E-HC-A3`** — No keyboard-flow assertions. Tab order, focus indicator visibility, focus return on modal close, Escape behavior, Enter/Space activation — axe does not catch these and a mouse-only audit is not an audit.
4. **`E-HC-A4`** — Asserts total violation count rather than violation IDs. A newly-introduced violation can hide a fix for an old one and the test stays green.
5. **`E-HC-A5`** — No WCAG level cited. "Passes axe" without declaring the conformance target (2.1 AA, 2.2 AA, etc.) is not a contract. It is a vibe.
6. **`E-HC-A6`** — Axe scoped to `<body>` when the page contains out-of-tree content (portals, tooltips, modals mounted to `document.body`). The scope is lying about what is actually on the page.

### 5.3 High-confidence smells, sub-lane P (performance budget) — `E-HC-P1..E-HC-P6`

1. **`E-HC-P1`** — Perf budget with no external source. A threshold like `LCP < 2500ms` with no citation to Web Vitals, a RUM baseline, or a team SLO is a pasted literal — the performance-layer analogue of [`HC-3`](unit-testing.md).
2. **`E-HC-P2`** — Single-sample assertion on a noisy metric. One LCP measurement is noise; a budget must assert against p75 or p95 across N runs with N and the percentile declared.
3. **`E-HC-P3`** — No cold-vs-warm distinction. A cached navigation is asserted at the same budget as a cold load; the test cannot tell the difference and the budget proves neither case.
4. **`E-HC-P4`** — Budget asserted against localhost. Localhost is faster than any real network; the assertion is a lower bound on production perf, not an upper bound. At best a smoke test, not a budget.
5. **`E-HC-P5`** — Perf test quarantined with a skip tag rather than having its flake root-caused. Quarantine is not a fix; it is a signal that the budget is wrong, the sampling strategy is wrong, or the metric is not deterministic under CI conditions.
6. **`E-HC-P6`** — Hardware or CI-shape assumption uncontrolled: CPU count, GPU availability, throttling profile, container CPU share. Perf budgets that pass on a developer laptop and fail on CI are measuring the hardware, not the code.

### 5.4 High-confidence smells, sub-lane S (security surface) — `E-HC-S1..E-HC-S5`

1. **`E-HC-S1`** — Asserts a security header's *value* only; does not exercise the browser's enforcement of it. If the test does not need a browser to prove anything, the test is in the wrong lane — move to integration sub-lane B. Sub-lane S exists to prove browser *behavior*, not response headers.
2. **`E-HC-S2`** — XSS or injection payload test with one pasted-literal payload and no reference to OWASP, a payload list, or a fuzzing corpus. The negative-space coverage is zero.
3. **`E-HC-S3`** — Auth test with only a happy-path valid session. No expired token, no missing token, no tampered cookie, no cross-user access attempt. (Browser-layer analogue of [`I-HC-B7`](integration-testing.md).)
4. **`E-HC-S4`** — CSP / CORS / frame-ancestors test asserts server response without verifying *browser-side* blocking. The contract under test is what the browser does with the policy, not what the server declared.
5. **`E-HC-S5`** — Security test that runs against a test-only endpoint, a debug route, or a build flavor that does not exist in production. Whatever it proves, it does not prove what production does. (Browser-layer analogue of [`I-HC-B2`](integration-testing.md).)

### 5.5 Low-confidence smells, shared across sub-lanes — `E-LC-1..E-LC-6`

1. **`E-LC-1`** — One giant test class covering journey, a11y, perf, and security concerns at once. Suggests no per-lane thinking and makes the suite harder to quarantine selectively.
2. **`E-LC-2`** — Test name ends in `_Works` / `_E2E` / `_Journey` / `_Integration` with no user outcome in the name.
3. **`E-LC-3`** — Parameterized test where every case asserts the same thing.
4. **`E-LC-4`** — Full stack launched for a one-line DOM-text assertion. May be a critical smoke test; may be incidental coverage with no stated outcome.
5. **`E-LC-5`** — Page Object with >20 methods covering multiple unrelated pages. Likely a god-object carrying multiple lanes' concerns.
6. **`E-LC-6`** — Test's only wait is a navigation predicate (URL matches or navigation event fires) with no subsequent content check. Navigation completed; whether the page loaded and rendered is unverified.

### 5.6 Positive signals (reward these) — `E-POS-1..E-POS-9`

1. **`E-POS-1`** — Test name reads as a user story. `Anonymous_User_Signs_In_And_Reaches_Authenticated_Home` beats `LoginFlowTest`.
2. **`E-POS-2`** — Locators derived from accessible role, accessible name, visible text, label association, or placeholder. No raw CSS or xpath in the test body.
3. **`E-POS-3`** — Condition-based waits only. No wall-clock sleeps, fixed-duration delays, or fixed-budget wait primitives anywhere in the test or its helpers.
4. **`E-POS-4`** — Perf budget cited against Web Vitals, a RUM baseline, or an SLO with a link in the test or a peer comment.
5. **`E-POS-5`** — A11y audit cites a WCAG conformance level, runs after interaction as well as at load, and any rule suppression has a linked justification.
6. **`E-POS-6`** — Per-test data, user, and session ownership. Each test creates its own fixtures and owns its own browser context.
7. **`E-POS-7`** — Traces, screenshots, DOM snapshots, console logs captured on failure *as diagnostics* — not consumed by any assertion.
8. **`E-POS-8`** — Hermetic stack bringup: containerized adjacent dependencies, dynamic ports, readiness waits, deterministic seed data. Test runs offline, in parallel with itself, twice in a row with the same result.
9. **`E-POS-9`** — Security test exercises a browser-specific enforcement path (CSP blocks an injected script, cookie jar respects `SameSite`, cross-origin iframe blocked by X-Frame-Options) rather than asserting a header value.

---

## 6. The test pyramid question — position

A recurring fault line in the literature. The audit takes a position rather than equivocate.

- **Cohn's Test Pyramid** places UI tests at the top: few, slow, expensive. The base is unit; the middle is service/integration. E2E is deliberately narrow.
- **Google Testing Blog (*Just Say No to More End-to-End Tests*)** takes the same shape further: the recommended mix is 70% unit / 20% integration / 10% E2E, and even that 10% is a ceiling, not a target.
- **Kent C. Dodds's Test Trophy** inverts slightly — more integration, similar caps on E2E, more emphasis on the middle lane because integration catches the most bugs per unit of maintenance cost.
- **Fowler (*TestPyramid*, *BroadStackTest*)** emphasizes that broad-stack tests have a place for the journeys that genuinely cross layers — but that the set of journeys that *need* the browser is much smaller than teams initially assume.

**Position:** E2E should be deliberately few, deliberately high-value, and deliberately non-overlapping with the integration lane. The default answer to "should we add an E2E test?" is **no** — unless the test proves a user outcome that no cheaper test can, and the team is willing to own its flake profile. A suite that grows linearly with feature count is a quarantined suite waiting to happen. A suite that grows sub-linearly — one test per distinct user journey, with features routed to unit and integration coverage — is the target.

The sub-lanes inherit this constraint differently. Sub-lane F should be small and bounded by distinct user journeys. Sub-lane A should be proportional to page count, not feature count. Sub-lane P should cover representative pages, not every page. Sub-lane S should cover the browser-specific enforcement surface, nothing more.

---

## 7. Flake, isolation, and environment

Flake at the E2E layer is the same failure mode as at the integration layer, amplified by the browser. Everything in [integration-testing.md §7](integration-testing.md) applies. This section lists the E2E-specific additions.

### 7.1 Additional sources of flake

- **Browser timing.** Animations, transitions, async renders, layout shift during load, debounced handlers. Any test that times against "the UI is ready" without a condition is asking the browser to race the assertion.
- **Network throttling.** A test asserted without a throttling profile is asserting against whatever the developer's connection happens to be. A test asserted *with* throttling must control it explicitly at the framework layer.
- **Viewport and scaling.** Elements rendered below the fold, elements clipped under a responsive breakpoint, focus indicators that move on zoom. Assertions that work at 1920×1080 can fail at 1280×720 and nobody will know why.
- **Third-party redirects and OAuth flows.** A real OAuth provider's login screen is outside the test's control. Sub-lane F tests that traverse a live third-party boundary are *not hermetic* and must either stub the provider at the network layer or use a provider-supplied test mode.
- **Service workers and HTTP cache.** A test that expects a cold load but runs in a warmed browser profile is asserting against garbage. Per-test browser contexts or explicit cache clears are the fix.
- **Input method assumptions.** A test that uses mouse click on a touch-first viewport, or tab order on a screen reader with a different focus model, may pass locally and fail in CI emulation.

### 7.2 Rules

- Every test owns its own browser context. No shared cookies, no shared localStorage, no shared session. The context is the E2E-layer analogue of integration sub-lane A's per-test data ownership.
- No wall-clock waits. Ever. If the framework cannot express the condition, the finding is the framework gap, not the test.
- Animations are disabled or controlled per test run (`prefers-reduced-motion`, framework-level animation flag, injected CSS).
- Third-party boundaries are stubbed at the network layer or served by a provider test mode. Live-provider E2E tests belong in a separate, infrequently-run lane — never the per-commit suite.
- Each test explicitly declares cold vs. warm load and controls the browser state accordingly.

### 7.3 Quarantine is not a fix

A quarantined E2E test is a scope failure the suite has learned to ignore. Quarantining compounds: a growing quarantine list is the strongest signal that the suite's scope is wrong somewhere — either tests are trying to prove things they cannot prove deterministically, or the lanes below E2E are not catching what they should. Route the fix to the root cause, not the `[Skip]` tag.

---

## 8. Coverage, runtime signals, and the limits of static audit

### 8.1 Signals static analysis can use

- **Wait primitives.** Wall-clock sleeps, fixed-duration delays, fixed-budget wait calls, retry-until-green loops — detectable as patterns.
- **Selector style.** Raw CSS selectors, xpath, test-ids-without-accessible-name — detectable lexically.
- **Snapshot usage.** Snapshot/golden-file assertions, saved-HTML diffs, visual-regression calls — high-confidence when no baseline workflow is referenced.
- **Assertion target.** Whether the test asserts on user-observable output or on URLs/DOM presence only.
- **Missing assertions.** A test that drives a flow and never calls an `Expect` / `Should` / `Assert` is detectable without execution.
- **Shared-state patterns.** Static `_browserContext`, test-class-level cookies, fixture reuse across unrelated tests — detectable from lifecycle hooks.
- **Perf-threshold provenance.** A numeric budget with no linked source, no percentile declaration, and no sample-count declaration — detectable by pattern.
- **A11y scan scope and citation.** Axe scan with no rule-set declaration, no WCAG level, or a suppression list with no linked justification — detectable by call shape.

### 8.2 Things static analysis cannot know (admit, do not fabricate)

- **Real-browser timing under CI load.** Whether a condition-based wait is actually fast enough to pass. Requires execution history.
- **Whether an axe rule suppression hides a real violation.** Requires a human review of each suppression against the current UI.
- **Whether a perf budget is achievable on production hardware.** Requires RUM or synthetic-monitoring data.
- **Whether a visual-regression baseline reflects current design intent.** Requires a baseline review process and an owner.
- **Whether the journey under test is a real user story.** Requires product context.
- **Flake rate and quarantine history.** Requires CI history and a test-result database.

### 8.3 Recommended runtime signals

- **Flake rate per test** over the last N runs. A test above 1% flake is a scope smell.
- **Time-to-green per test** — how long a failing E2E test takes to diagnose in practice. Longer-than-average times correlate with weak intent statements and snapshot-style assertions.
- **Historical failure attribution.** What did the E2E suite catch in the last N weeks that unit and integration did not? If the answer is "nothing," the suite is a candidate for aggressive pruning.
- **Perf budget trend data.** Per-run artifacts feeding a trend graph. A per-run artifact that nobody reviews is noise; a trend graph with an owner is a signal.
- **A11y scan deltas.** New violations over time, by rule ID — not a single total count.

Be explicit about the limits. Ask for runtime signals rather than invent certainty.

---

## 9. Per-test audit rubric

For each E2E test case, an audit should produce:

1. **User-outcome statement.** In one sentence: what does the user accomplish or experience through this test? If unarticulable, that itself is a finding and the provenance question in §1 has failed.
2. **Scope statement.** In one sentence: why couldn't a cheaper test (unit or integration) prove the same thing? If unarticulable, the test fails the scope question in §1.
3. **Sub-lane.** `F` / `A` / `P` / `S`.
4. **Test size** (Google sizing). Small / medium / large. E2E is almost always `large`.
5. **Selector provenance.** `accessible-name` / `test-id-with-accessible-fallback` / `test-id-only` / `css-or-xpath` / `mixed`.
6. **Wait strategy.** `condition-based` / `mixed` / `wall-clock` / `none`.
7. **Fixture and data provenance.** `per-test-factory` / `shared-immutable` / `shared-mutable` / `external-snapshot` / `pasted-literal` / `unknown`.
8. **Assertion target.** `user-observable-outcome` / `url-only` / `element-presence-only` / `snapshot` / `pixel-baseline` / `none`.
9. **Smells matched** from §5.1, §5.2, §5.3, §5.4, §5.5 (cite codes: `E-HC-F3`, `E-HC-A1`, `E-LC-2`, etc.).
10. **Positive signals matched** from §5.6.
11. **Verdict.** `specification` / `characterization` / `incidental` / `ambiguous`. The unit rubric's `characterization` and the integration rubric's `incidental` both apply at the E2E layer — the first for tests that pin observed UI output, the second for tests with no defensible scope.
12. **Severity.** `block` / `warn` / `info`.
13. **Recommended action.** `rewrite-from-user-story` / `add-assertion` / `split` / `narrow-the-journey` / `move-to-integration-lane` / `move-to-unit-lane` / `replace-selector-strategy` / `replace-wait-strategy` / `delete` / `keep`.

The rubric is deliberately opinionated. Soft audits produce ignorable noise — and at the E2E layer, ignorable noise becomes quarantine within a quarter.

---

## 10. Directive principles

Distilled from §2–§7, written as directives an audit agent can apply directly.

1. **An E2E test must state both its user outcome and why a cheaper test wouldn't do.** A test that fails either question is a smell regardless of how green it runs. Scope and provenance together; neither alone is sufficient.
2. **Default to no.** The decision to add an E2E test is a decision to accept ongoing flake risk and CI cost. Route to unit or integration first; promote to E2E only when the user outcome genuinely requires the browser.
3. **Name tests as user stories, not UI steps.** The name is the provenance check.
4. **Use accessible-name selectors.** Implementation selectors are characterization in disguise.
5. **Condition-based waits only.** Wall-clock waits are flake in slow motion, doubly disqualifying at the E2E layer.
6. **Per-test browser context.** Shared browser state is the E2E analogue of shared mutable fixtures and fails in the same way.
7. **Snapshot equals characterization, amplified.** DOM snapshots and pixel baselines are valid only with a review gate, an owner, and a drift process. Without that scaffolding, they pin today's build.
8. **Perf budgets cite an external source.** Web Vitals thresholds, RUM baselines, or a team SLO. A threshold with no source is a pasted literal.
9. **A11y audits cite a WCAG conformance level.** "Passes axe" without a level is not a contract. Rule suppressions carry linked justifications.
10. **Security tests exercise browser enforcement.** Header-value assertions belong in integration sub-lane B. Sub-lane S exists to prove that the browser does what the header claims.
11. **Quarantine is a scope signal, not a fix.** A growing quarantine list is a design finding for the suite, not a backlog item.
12. **Be honest about what cannot be determined from source alone.** Flake rate, real-browser timing, perf achievability, visual drift ownership — ask, do not guess.

---

## 11. Sources

### Martin Fowler
- [TestPyramid](https://martinfowler.com/bliki/TestPyramid.html)
- [BroadStackTest](https://martinfowler.com/bliki/BroadStackTest.html)
- [Testing resource index](https://martinfowler.com/testing/)

### Test Pyramid and Test Trophy
- Mike Cohn, *Succeeding with Agile* — origin of the Test Pyramid.
- Kent C. Dodds, [*The Testing Trophy and Testing Classifications*](https://kentcdodds.com/blog/the-testing-trophy-and-testing-classifications) — the "more integration, fewer E2E" shape.

### Google Testing
- Winters, Manshreck, Wright — *Software Engineering at Google*, ch. 11 ("Testing Overview"), for the small / medium / large test sizing taxonomy and the recommended mix.
- [Just Say No to More End-to-End Tests](https://testing.googleblog.com/2015/04/just-say-no-to-more-end-to-end-tests.html) — the canonical argument for keeping E2E deliberately small.
- [Hermetic Servers](https://testing.googleblog.com/2012/10/hermetic-servers.html) — applies doubly when the browser is in the loop.

### Playwright, Cypress, WebDriverIO best practices
- [Playwright: Best Practices](https://playwright.dev/docs/best-practices) — accessible-name selectors, auto-retrying assertions, test isolation.
- [Playwright: Locators](https://playwright.dev/docs/locators) — the role-based locator hierarchy.
- [Cypress: Best Practices](https://docs.cypress.io/guides/references/best-practices) — selector strategy and flake avoidance.
- [WebDriverIO: Best Practices](https://webdriver.io/docs/bestpractices/) — condition-based waits and hermetic setup.

### Accessibility
- [Web Content Accessibility Guidelines (WCAG) 2.2](https://www.w3.org/TR/WCAG22/) — the conformance target canonical source.
- [axe-core](https://github.com/dequelabs/axe-core) and [Deque University](https://dequeuniversity.com/) — the rule set most teams actually audit against, plus the documentation of its limits (keyboard flow and focus management are not axe's job).

### Performance
- [web.dev — Core Web Vitals](https://web.dev/articles/vitals) — canonical thresholds for LCP, INP, CLS.
- [web.dev — Defining the Core Web Vitals metrics thresholds](https://web.dev/articles/defining-core-web-vitals-thresholds) — the reasoning behind the numbers, for budget provenance.

### Security
- [OWASP Application Security Verification Standard (ASVS)](https://owasp.org/www-project-application-security-verification-standard/) — the conformance target for security tests that earn a row in the suite.
- [OWASP Testing Guide](https://owasp.org/www-project-web-security-testing-guide/) — browser-level attack-surface coverage.
- [MDN — Content Security Policy](https://developer.mozilla.org/docs/Web/HTTP/CSP) and [MDN — Cookies](https://developer.mozilla.org/docs/Web/HTTP/Cookies) — reference for the browser-enforcement paths sub-lane S actually exercises.

### Sibling references
- [unit-testing.md](unit-testing.md) — the unit rubric this document composes with. Where the E2E rubric is silent on a topic that applies to all lanes (assertion specificity, FIRST properties, the limits of coverage), defer to the unit-testing reference.
- [integration-testing.md](integration-testing.md) — the integration rubric this document composes with. Where the E2E rubric is silent on flake, hermeticity, or per-test data ownership, the integration-testing reference is authoritative; §7 of this document lists only the E2E-specific additions.
