# Frontend Responsiveness Performance Tests

**Date:** 2026-03-24
**Status:** Draft

---

## Overview

Add a browser-only frontend responsiveness test layer on top of the existing Playwright E2E stack in `frontend/`.

This plan is driven by three priorities, in order:

1. In-app interactions
2. Initial page load
3. Mobile runtime behavior

All user flows are treated as critical. Async operations may take longer than synchronous UI updates, but only if the UI acknowledges the action quickly with a visible waiting indicator.

---

## Goals

- Catch regressions where user input appears ignored
- Catch regressions where route changes or data updates become visibly janky
- Catch regressions where async actions have no immediate loading feedback
- Establish browser-measured budgets that can eventually act as CI gates
- Reuse the existing Playwright harness instead of introducing a second performance toolchain

---

## Non-Goals

- Field RUM collection
- Lighthouse-only auditing as the primary gate
- Synthetic component-level profiling outside a real browser
- Exhaustive cross-browser coverage in the first iteration

---

## Standards Baseline

Industry-standard thresholds should inform the initial budgets, but they are not sufficient on their own for this app because they do not assert that each async user action shows immediate feedback.

Relevant baseline metrics:

- `INP`: good at `<= 200 ms`
- `LCP`: good at `<= 2.5 s`
- `CLS`: good at `<= 0.1`

These come from the current Core Web Vitals guidance and related documentation:

- web.dev Core Web Vitals and INP guidance
- web.dev LCP guidance
- MDN glossary/docs for LCP and CLS
- Chrome for Developers guidance on lab metrics such as `TBT`

These standards should be used as outer constraints for page-level responsiveness. Flow-level acceptance criteria still need app-specific budgets.

---

## Recommended Approach

Use Playwright flow budgets with browser performance APIs.

Each performance spec should:

1. Navigate to a real app state using the current E2E harness
2. Install browser-side observers before the measured interaction
3. Trigger a real user action
4. Assert immediate visual acknowledgement
5. Assert bounded completion time for the flow
6. Assert stability during the measured window

This approach fits the repo best because:

- Playwright is already installed and in use
- The repo already has end-to-end fixtures and seeded scenarios
- The highest priority is in-app interaction responsiveness, not just page audit scores
- Browser-only measurement matches the requested scope

---

## Failure Model

An interaction should fail the test if any of the following happen:

- No visible response is shown within the immediate-feedback budget
- The interaction produces obvious instability, such as major layout shifts or long main-thread blocking
- The operation does not complete within the completion budget

For async operations, quick acknowledgement is mandatory even when overall completion is slower.

Accepted acknowledgement examples:

- Spinner or progress indicator becomes visible
- Submit button becomes disabled with visible busy state
- Skeleton content appears
- Route transition shell appears
- Optimistic UI state appears

---

## Measurement Model

All measurements should be browser-derived and captured inside Playwright tests.

### 1. Immediate Feedback

Measure the time from the user action until the first meaningful visible acknowledgement.

Examples:

- Click on raid card until detail route shell or heading starts rendering
- Click submit until button enters busy state
- Choose a character until selection state visibly updates

Primary assertion:

- Every interaction must produce an observable UI response quickly, even if backend work continues

### 2. Completion

Measure the time from the user action until the expected stable success or error state is rendered.

Examples:

- Signup click until confirmation or updated roster state is visible
- Create raid submit until redirect or created-raid detail page is visible
- Login callback flow until logged-in landing state is visible

Primary assertion:

- Async flows may take longer than the immediate-feedback budget, but they still require a bounded completion window

### 3. Stability

Measure whether the interaction window contains signs of jank.

Primary signals:

- `layout-shift` entries during the interaction window
- Long tasks on the main thread during the interaction window when available
- Playwright-visible delays between input and next paint-like DOM changes

Primary assertion:

- Critical flows should not visibly jump or freeze while handling input

---

## Initial Budget Classes

These are starting targets for the first draft of the suite. They should be treated as provisional until the first baseline run is collected.

### Interaction Budgets

- Immediate visual acknowledgement for ordinary actions: `<= 200 ms`
- Immediate visual acknowledgement for heavier UI transitions: `<= 300 ms`

Rationale:

- Aligns with the `INP <= 200 ms` standard where feasible
- Allows a small margin for SPA transitions that still feel responsive

### Completion Budgets

- Fast in-app transitions with local or cached data: `<= 1.0 s`
- Network-backed updates with visible loading state: `<= 2.0 s`
- Slower but still acceptable flows under mobile emulation: `<= 3.0 s`

Rationale:

- Keeps the gate focused on user-perceived responsiveness, not raw backend speed alone
- Preserves a distinction between acknowledgement and completion

### Stability Budgets

- No large unexpected layout shifts during measured interactions
- Cumulative shift during a measured interaction window should remain materially below page-level `CLS` failure territory
- No repeated long tasks that obviously block the main thread during critical actions

The exact numeric long-task ceiling should be finalized after the first instrumentation pass, because browser support and CI noise need to be validated against the existing harness.

## Threshold Policy

Apply different rollout strictness to different budget types.

### Hard Gate From First Implementation

- Immediate acknowledgement budget
- Visible loading-state requirement for async actions
- No major visible instability during the measured interaction window

These are product requirements, not optimization niceties. If a flow does not acknowledge user input quickly, the test should fail immediately.

### Baseline First, Then Hard Gate

- Completion time budgets
- Numeric long-task ceilings
- Mobile-specific completion thresholds

These should be measured on the first local and CI runs before being enforced as hard thresholds. The final numbers should stay close to the current proposed targets unless the baseline shows the harness itself is too noisy.

---

## Proposed Spec Structure

Create a separate performance-focused Playwright area alongside the current E2E specs.

Suggested layout:

```text
frontend/
  e2e/
    perf/
      helpers/
        performanceObservers.ts
        flowBudgets.ts
        perfAssertions.ts
      navigation.perf.spec.ts
      forms.perf.spec.ts
      async-actions.perf.spec.ts
      mobile.perf.spec.ts
      load.perf.spec.ts
```

### `navigation.perf.spec.ts`

Focus:

- Landing page to login entry
- Raids list to raid detail
- Return navigation where applicable

Checks:

- First visible route acknowledgement
- Stable route completion
- No major layout jumps during transition

### `forms.perf.spec.ts`

Focus:

- Create raid form interactions
- Date/time input interactions
- Validation response timing

Checks:

- Input-driven feedback appears promptly
- Validation errors or success state render quickly
- No blocking/janky behavior while editing complex controls

### `async-actions.perf.spec.ts`

Focus:

- Raid signup
- Cancel signup
- Character-dependent async operations

Checks:

- Busy state appears quickly
- Completion stays within the flow budget
- Error states also satisfy acknowledgement rules

### `mobile.perf.spec.ts`

Focus:

- A mobile-emulated subset of the most important flows

Checks:

- Same acknowledgement and completion rules under smaller viewport and slower device conditions
- No mobile-only layout jumps or frozen transitions

### `load.perf.spec.ts`

Focus:

- Initial page load for the most important entry points

Checks:

- Existing first-paint shell guarantee remains intact
- Browser-observable page-level responsiveness remains within initial budget targets

---

## Vertical Slice Rollout

Build the suite by user-flow slices instead of by test category. Each slice should include the helper wiring, desktop assertions, and mobile coverage where it adds real signal.

### Slice 1: Raid Navigation

Flow:

- Raids list load
- Raids list to raid detail navigation

Measures:

- Immediate route acknowledgement
- Stable detail render completion
- Layout stability during transition

Why first:

- Low ambiguity
- High user value
- Good place to prove the core harness

### Slice 2: Raid Signup

Flow:

- Signup action from an eligible raid state
- Cancel-signup action from a signed-up state

Measures:

- Busy/loading acknowledgement after click
- Completion to updated roster or confirmation state
- Stability while async work is in flight

Why second:

- Directly exercises the "longer wait is fine only with visible feedback" rule

### Slice 3: Create Raid

Flow:

- Create raid form interaction
- Validation responses
- Submit to created raid or success state

Measures:

- Responsiveness of complex controls
- Prompt validation feedback
- Submit acknowledgement and completion

Why third:

- Covers the heaviest in-app form flow

### Slice 4: Entry and Load

Flow:

- Landing page load
- Login entry and login-success transition

Measures:

- First-paint shell integrity
- Initial load responsiveness
- Transition acknowledgement into authenticated state

Why fourth:

- Important, but lower priority than in-app interaction flows

### Slice 5: Mobile Runtime

Flow subset:

- Raid navigation
- Signup

Measures:

- Same acknowledgement and completion rules under mobile emulation
- Mobile-specific layout stability

Why fifth:

- Keeps mobile in scope without slowing down the first harness iteration

This rollout still respects the requirement that every flow is critical. The difference is execution order: prove the harness on one full vertical slice, then apply the same contract across the remaining flows.

## Execution Guidance

The implementation plan for this design should assume parallel execution using separate git worktrees.

### Parallelization Model

Use one coordination thread plus multiple implementation agents working in parallel on disjoint write scopes.

Recommended split:

- Worktree A: shared performance helpers and base harness wiring under `frontend/e2e/perf/helpers`
- Worktree B: Slice 1 raid-navigation performance specs
- Worktree C: Slice 2 signup and cancel-signup performance specs
- Worktree D: Slice 3 create-raid performance specs
- Worktree E: Slice 4 entry/load performance specs and any `playwright.config.ts` updates
- Worktree F: Slice 5 mobile-emulation specs after the desktop harness is proven

### Ownership Rules

Each agent should own a narrow, explicit file set.

Examples:

- Shared-helper agent owns `frontend/e2e/perf/helpers/**`
- Navigation agent owns `frontend/e2e/perf/navigation.perf.spec.ts`
- Signup agent owns `frontend/e2e/perf/async-actions.perf.spec.ts`
- Create-raid agent owns `frontend/e2e/perf/forms.perf.spec.ts`
- Load/mobile agent owns `frontend/e2e/perf/load.perf.spec.ts`, `frontend/e2e/perf/mobile.perf.spec.ts`, and any related config changes assigned to it

Avoid overlapping edits unless the coordinator has already landed or manually integrated the shared helper contracts.

### Dependency Order

Parallel work should still respect one dependency:

- Shared helper contracts should be defined first

After that, slice implementations can proceed in parallel against the agreed helper surface. If helper signatures change, the coordinator should update the plan and communicate the new contract before additional slice work continues.

### Integration Order

Merge or cherry-pick work back in this order:

1. Shared helpers and base harness
2. Navigation slice
3. Signup slice
4. Create-raid slice
5. Entry/load slice
6. Mobile slice

This minimizes integration conflicts and ensures later slices build on the same measurement model.

### Verification Expectations

Each worktree should verify only the slice it owns plus any shared-helper tests it depends on.

The coordinator should run:

- slice-local verification after each integration
- a combined Playwright performance run after desktop slices land
- a combined desktop + mobile run before enabling CI gating

### Plan Readiness Requirement

Do not start parallel implementation until the written implementation plan explicitly:

- assigns ownership by worktree
- defines the shared helper contract
- defines integration order
- defines verification commands per slice

If any of those are missing, the implementation plan is not ready and must be updated before execution starts.

## Slice Contracts

Each vertical slice needs explicit acknowledgement and completion markers so the tests measure visible responsiveness rather than inferred state.

### Slice 1: Raid Navigation

Flow:

- Open raids list
- Select a raid
- Wait for raid detail view

Acknowledgement marker:

- Route shell changes, or a raid detail heading/container becomes visible

Completion marker:

- Raid detail heading is visible and key summary content has rendered

Failure examples:

- Click appears ignored
- Route transition starts too late
- Detail view arrives with visible jumping or blocking

### Slice 2: Raid Signup

Flow:

- Trigger signup from a raid state where signup is allowed

Acknowledgement marker:

- Signup control becomes disabled, busy, or shows a visible loading indicator

Completion marker:

- Roster or signup controls reflect the signed-up state, or a visible error state is rendered

Failure examples:

- Button click produces no immediate visible response
- Spinner appears too late
- Updated raid state takes too long to become trustworthy

### Slice 2b: Cancel Signup

Flow:

- Trigger cancel-signup from a state where the user is already signed up

Acknowledgement marker:

- Cancel control becomes disabled, busy, or shows a visible loading indicator

Completion marker:

- Roster or action controls reflect the cancelled state, or a visible error state is rendered

Failure examples:

- No quick acknowledgement after click
- Completion lags even though the request eventually succeeds

### Slice 3: Create Raid

Flow:

- Interact with the create-raid form
- Submit valid data

Acknowledgement marker:

- Validation feedback appears quickly for invalid input, or the submit control enters a visible busy state for valid submit

Completion marker:

- Redirect to the created raid view, or a stable success state is visible

Failure examples:

- Date/time or other complex controls feel blocked
- Submit appears ignored
- Success state is delayed without immediate acknowledgement

### Slice 4: Entry and Load

Flow:

- Load landing page
- Enter login path
- Complete login-success transition where covered by the harness

Acknowledgement marker:

- Document shell or transition shell is visible immediately

Completion marker:

- Intended entry state is visibly ready for use

Failure examples:

- Blank or unstable initial paint
- Login entry transition appears inert

### Slice 5: Mobile Runtime

Flow subset:

- Raid navigation
- Signup

Acknowledgement marker:

- Same visible markers as desktop

Completion marker:

- Same semantic completion markers as desktop

Difference from desktop:

- Separate thresholds tuned for mobile emulation
- Additional scrutiny on layout stability in narrow viewports

---

## Browser Instrumentation Strategy

Use browser APIs from inside Playwright instead of external profilers.

Candidate techniques:

- `PerformanceObserver` for `layout-shift`
- `PerformanceObserver` for `largest-contentful-paint` on initial-load specs
- Long-task observation where supported by the browser context used in Playwright
- DOM-state timestamps around visible acknowledgement and stable completion
- Playwright trace artifacts retained on failure for debugging regressions

Important constraint:

- The tests should assert user-visible responsiveness, not implementation details such as React lifecycle timing

## Spec Contract

Each performance spec should follow the same contract so results stay comparable across flows.

### Setup

- Start from a deterministic seeded scenario
- Install browser observers before the measured action
- Identify two DOM markers:
  - acknowledgement marker
  - stable completion marker

### Action

- Trigger one real user input sequence only
- Avoid combining multiple interactions into one timing window

### Assertions

- Acknowledgement marker appears within the acknowledgement budget
- If the flow is async, a visible busy/loading state appears before waiting for completion
- Stable completion marker appears within the completion budget
- Stability signals remain within allowed thresholds during the measured window

### Failure Output

On failure, the test should retain enough data to explain whether the problem was:

- no acknowledgement
- slow acknowledgement
- slow completion
- layout instability
- main-thread blocking

Playwright trace retention on failure should remain enabled so regressions are debuggable.

## Helper Design

Shared helpers should make flow specs short and consistent.

Suggested helper responsibilities:

- Start and stop browser observers for one measured window
- Return normalized timing data for acknowledgement, completion, layout-shift summary, and long-task summary
- Provide reusable assertions for common budget types
- Keep test code focused on the flow, not observer boilerplate

Suggested helper surface:

- `beginInteractionMeasurement(page, options)`
- `endInteractionMeasurement(page)`
- `expectAcknowledgementWithin(result, budget)`
- `expectCompletionWithin(result, budget)`
- `expectStableInteraction(result, budget)`

The exact helper names can change, but the abstraction should stay centered on user-flow measurements rather than raw Performance API details.

## Instrumentation Details

Use simple, explicit signals.

### Acknowledgement Marker Selection

Prefer markers that are directly visible to the user:

- button busy state
- spinner or skeleton visibility
- route heading change
- success/error banner appearing
- optimistic roster update

Do not use hidden internal state or network completion as the acknowledgement signal.

### Completion Marker Selection

Completion should mean the user can trust the new state.

Examples:

- raid detail heading is visible and settled after navigation
- signup state is reflected in the roster or action controls
- created raid page or success state is visible after submit

### Stability Window

The stability window should begin immediately before the action and end when the completion marker is reached.

During that window, capture:

- layout-shift entries
- long-task entries where supported
- any flow-specific visible jump conditions that can be asserted directly

## CI Reporting Expectations

Performance failures should be actionable, not vague.

Each failed assertion should report:

- flow name
- measured acknowledgement time
- measured completion time when applicable
- layout-shift summary
- long-task summary when available
- which threshold was exceeded

If numeric observer data is unavailable in a specific browser context, the suite should still fail on missing acknowledgement or missed completion markers. Observer limitations are not a reason to weaken visible responsiveness checks.

---

## CI Gating Strategy

Start with hard budgets in the test suite only after the first baseline run is collected locally and in CI.

Recommended rollout:

1. Implement the harness and record baseline results
2. Set thresholds slightly above the stable baseline while preserving the design intent
3. Enable as CI gates for the covered flows
4. Expand coverage gradually rather than shipping an unstable all-flows gate at once

This keeps the suite credible. A noisy performance gate will be ignored.

---

## Risks

- Playwright timing assertions can be noisy across environments if budgets are too tight
- Mobile emulation can reveal slower timings that require separate thresholds from desktop
- Some current flows may not expose a reliable visible busy state; those flows should be fixed in the UI rather than weakening the test
- Long-task observation support may vary enough that DOM-visible timing assertions remain the primary signal

---

## Implementation Outline

1. Add shared browser-observer helpers under `frontend/e2e/perf/helpers`
2. Implement Slice 1 and validate the timing model
3. Implement Slice 2 and confirm the loading-indicator contract works in practice
4. Implement Slice 3 for the heaviest form flow
5. Add Slice 4 load coverage
6. Add Slice 5 mobile coverage
7. Establish baseline timings locally and in CI
8. Finalize thresholds and enable gating
9. Extend the same harness to the remaining flows

This outline should be converted into a worktree-aware implementation plan before coding begins.

---

## Open Questions

- Which exact flows should be in Phase 1 if the suite must stay short?
- Should mobile performance specs run on every PR or only on main/nightly?
- Which current flows lack a sufficiently explicit waiting indicator and need product changes before they can pass the desired gate?

---

## Current Recommendation

Proceed by vertical slices in this order:

- Raid navigation
- Raid signup and cancel-signup
- Create raid interaction and submit
- Landing/login entry load and transition
- Mobile-emulated navigation and signup

Use strict acknowledgement budgets from the start and provisional completion budgets until baseline data is collected.
