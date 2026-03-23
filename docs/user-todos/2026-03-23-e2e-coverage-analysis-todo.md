# E2E Coverage Analysis Todo

## Current suite shape

- Playwright e2e coverage currently lives under `frontend/e2e/`.
- Default discovery covers 16 tests in 9 spec files.
- Scenario-specific coverage adds 4 more specs that are intentionally excluded from default discovery and run through `./scripts/e2e-all.sh`.
- In total, the repo currently has 20 browser tests across 13 spec files.
- Coverage is concentrated on auth entry, the raids surface, and a few empty/error scenarios.

## What is covered well

### Auth and route protection

- `access-control.spec.ts` covers redirect behavior for unauthenticated users.
- `login-entry.spec.ts` covers local test-mode login, logout, callback failure, and the branch where a raider must choose a character before continuing.
- `landing-page.spec.ts` covers the public root route.

This means the suite has good browser coverage for the main public-to-authenticated entry path without relying on real Battle.net.

### Raids browsing and interaction

- `raids.spec.ts` covers authenticated raids browsing, pagination, query targeting, and a mobile compact/expand behavior.
- `raids-localized-names.spec.ts` covers a regression-prone rendering path for localized API names.
- `signup.spec.ts` covers create, update, and cancel signup behavior from the combined raids page.
- `create-raid.spec.ts` covers the create-raid happy path and form validation.

This is currently the strongest area of e2e coverage. The main raids page is exercised as a real user journey, not just as isolated page loads.

### Resilience and scenario states

- `raids-empty.spec.ts` covers no-raids empty state.
- `raids-error.spec.ts` covers raids load failure handling.
- `characters-empty.spec.ts` covers the no-characters empty state.
- `create-raid-instances-missing.spec.ts` covers instance lookup failure on create-raid.

This gives the suite useful coverage for the major scenario-driven branches in seeded local data.

### Quality smoke checks

- `a11y.spec.ts` covers keyboard reachability and axe checks for login, raids list, and combined raid detail.
- `first-paint.spec.ts` covers the shell-first paint behavior before app bootstrap.

These are valuable smoke checks, but they are still narrow compared with the total interaction surface.

## Coverage gaps

### 1. Characters happy paths are thin

The suite covers the branch where a user without a selected character is routed through character selection, and it covers the empty-state case on `/characters`. It does not cover the normal non-empty `/characters` page as a primary user flow.

Missing coverage likely includes:

- selecting or changing a main character from a populated character list
- refreshing characters from Battle.net-backed data in local test mode
- returning from character selection into the intended destination as a complete happy path

### 2. Raid lifecycle coverage is incomplete

The suite covers raid creation and signup lifecycle, but not the rest of the raid lifecycle.

Missing coverage likely includes:

- updating an existing raid
- deleting a raid
- permission and ownership behavior around raid mutation
- viewing a raid after state transitions other than signup changes

Backend endpoints for `raids-update` and `raids-delete` exist, but there is no corresponding Playwright coverage today.

### 3. Account and identity management flows are not covered end-to-end

There is no browser coverage for:

- account deletion / `me-delete`
- successful callback completion to a post-login success state
- non-happy-path identity transitions beyond the existing login-failed route

Some of these may be intentionally left to lower-level tests, but they are still user-visible flows with no current e2e protection.

### 4. Accessibility coverage is smoke-level, not journey-level

The current axe and keyboard checks are useful, but they do not cover:

- modal or confirmation flows after mutation actions
- create-raid form completion end-to-end from an accessibility perspective
- signup update and cancel dialogs after state changes
- the `/characters` page in its non-empty state

### 5. Browser and device coverage is intentionally narrow

The Playwright config currently runs only Chromium. There is one mobile-layout assertion in `raids.spec.ts`, but there is no broader mobile journey coverage and no multi-browser coverage.

That is a reasonable tradeoff for local speed, but it should be treated as a known limit rather than assumed broad client coverage.

## Coverage assessment

- Strong: login entry, route protection, raids browsing, signup lifecycle, create-raid success path, core empty/error branches.
- Moderate: accessibility smoke checks, mobile layout behavior, localized-name regression protection.
- Weak: characters happy paths, raid update/delete lifecycle, account deletion, broader success/failure identity flows, multi-browser confidence.

Overall, the suite gives meaningful protection for the app's main raids-centric workflow, but it does not yet provide balanced coverage across all authenticated user surfaces.

## User todos

- Add a populated `/characters` happy-path e2e that covers selecting or changing the active character and returning to the intended route.
- Add raid lifecycle coverage for update and delete behavior if those actions remain supported in the product.
- Add at least one e2e covering permission boundaries around raid mutation, not just happy-path ownership.
- Add an account-management decision: either cover `me-delete` in Playwright or explicitly document why unit/integration coverage is considered sufficient.
- Expand accessibility checks to at least one mutation-heavy flow such as create-raid or signup update/cancel.
- Decide whether successful login callback behavior needs an explicit e2e, or whether the current test-mode login coverage is the intended substitute.
- Keep the suite raids-centric, but avoid letting `/characters` remain effectively protected only by empty-state and redirect coverage.
