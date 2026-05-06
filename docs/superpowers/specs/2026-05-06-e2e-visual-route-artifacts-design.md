<!--
SPDX-License-Identifier: AGPL-3.0-or-later
SPDX-FileCopyrightText: 2026 LFM contributors
-->

# E2E Visual Route Artifacts - Design

**Date:** 2026-05-06
**Author:** Codex (brainstormed with user)
**Status:** spec -> implementation plan pending

## Goal

Add browser-level visual evidence for every Blazor route across desktop and
mobile responsive states. The suite should produce local screenshots and a
machine-readable index while still failing on browser-observable defects:
wrong redirects, missing route content, console errors, failed requests,
layout overflow, layout overlap, or artifact write failures.

This is a full-E2E artifact category and health check, not a pixel-diff visual
regression system or a separate workflow level.

## Approved Decisions

- Cover every app route.
- Cover both anonymous behavior and intended authenticated or authorized page
  state for protected routes.
- Capture local artifacts only. Do not commit image baselines.
- Use semantic health checks plus screenshots, not screenshot-only tests.
- Run across desktop, phone, and mobile-floor viewports.
- Include default theme, Finnish text expansion, dark mode, and forced-colors
  variants.

## Route Scope

The initial route manifest covers these concrete route states:

| Route | Anonymous state | Intended state |
|---|---|---|
| `/` | public landing page | same public state |
| `/login` | public login page | same public state |
| `/privacy` | public privacy page | same public state |
| `/login/failed` | public login failure page | same public state |
| `/auth/failure` | public auth failure page | same public state |
| `/not-found` | public not-found page | same public state |
| `/goodbye` | public goodbye page | same public state |
| `/runs` | redirect to `/login?redirect=%2Fruns` | authenticated runs list |
| `/runs/{RunId}` | redirect to `/login?redirect=%2Fruns%2Fe2e-run-001` | authenticated run detail for `DefaultSeed.TestRunId` |
| `/runs/new` | redirect to `/login?redirect=%2Fruns%2Fnew` | authenticated create-run form |
| `/runs/{RunId}/edit` | redirect to `/login?redirect=%2Fruns%2Fe2e-run-001%2Fedit` | authenticated edit-run form for `DefaultSeed.TestRunId` |
| `/characters` | redirect to `/login?redirect=%2Fcharacters` | authenticated characters page |
| `/guild` | redirect to `/login?redirect=%2Fguild` | authenticated guild page |
| `/guild/admin` | redirect to `/login?redirect=%2Fguild%2Fadmin` | site-admin guild-admin page |
| `/admin/reference` | redirect to `/login?redirect=%2Fadmin%2Freference` | site-admin reference-refresh page |
| `/instances` | redirect to `/login?redirect=%2Finstances` | authenticated instances page |

Parameterized routes use deterministic seed data. The manifest owns the
concrete URL, access mode, ready selector, and optional page setup for each
state.

## Responsive And Preference Matrix

Each route state runs through three viewport profiles:

| Name | Size | Purpose |
|---|---:|---|
| `desktop` | `1366x768` | ordinary web layout |
| `phone` | `390x844` | common modern phone viewport |
| `mobile-floor` | `320x568` | WCAG reflow floor and smallest supported screen |

Each viewport runs through four variants:

| Name | Browser settings |
|---|---|
| `default` | default locale and color settings |
| `fi` | `Locale = "fi-FI"` for Finnish text expansion |
| `dark` | dark color scheme |
| `forced-colors` | forced-colors active |

The matrix deliberately overlaps with the existing layout-integrity E2E
category, but serves a different purpose: it emits inspectable screenshots for
every route state while layout-integrity remains the deeper geometry-focused
assertion category.

## Implementation Shape

Add a dedicated E2E spec, tentatively `VisualRouteArtifactsSpec`, instead of
expanding the existing accessibility, navigation, performance, or
layout-integrity specs. Tag it as `VisualArtifacts` so targeted local runs can
request only screenshots, while the GitHub Actions workflow folds it into the
explicit `full` level.

Use a typed route manifest. Each entry declares:

- display name and screenshot-safe slug
- route template and concrete URL
- required access mode: public, authenticated, or site-admin
- anonymous expectation: rendered page or redirect
- route-specific ready check using user-observable roles, headings, or controls
- optional setup, such as selecting a run, loading guild admin data, stubbing
  portraits, or selecting dungeon mode

Use a helper for matrix execution and artifact writing. The helper creates a
fresh browser context per matrix entry with the requested viewport, locale,
color scheme, and forced-colors settings. It authenticates only when the
manifest state requires it.

For each matrix entry:

1. Navigate to the route or verify the expected anonymous redirect.
2. Wait for route-specific readiness.
3. Wait for network and font stability.
4. Run `LayoutIntegrityHelper.AssertNoOverlapsAsync`.
5. Fail on unexpected console errors, warnings, request failures, or failed
   artifact capture.
6. Capture a full-page screenshot.
7. Append an index entry describing the route, state, URL, viewport, variant,
   identity state, screenshot path, and status.

## Artifact Contract

Screenshots are written under:

`artifacts/e2e-results/visual-routes/<variant>/<viewport>/<route-state>.png`

The JSON index is written under:

`artifacts/e2e-results/visual-routes/index.json`

Artifacts remain gitignored through the existing `artifacts/` ignore rule. CI
already uploads `artifacts/e2e-results`, so these screenshots become available
with the normal E2E artifacts without committing image files.

The index should be deterministic and inspectable. It should contain enough
metadata for a reviewer to understand what was captured without opening every
file name manually.

## Boundaries

This belongs in E2E because it proves browser-only behavior: route rendering,
auth redirects, responsive layout, user preference rendering, text expansion,
and screenshot artifact capture. It must not duplicate API, app-core, or bUnit
business assertions.

Ready checks are shallow and user-observable. They prove that the target state
rendered and is usable enough to screenshot; they do not re-test full workflows.

No visual baselines or image diffs are part of this pass. A later task can add
opt-in baselines for a small subset of stable critical screens after the local
artifact lane has proven stable.

## Verification

Implementation verification should include:

- `bash ./scripts/check-e2e-drift.sh`
- targeted E2E run for the new visual artifact category
- `dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release` before claiming full
  E2E health
- `dotnet format lfm.sln --verify-no-changes --no-restore --severity error`
- `dotnet build lfm.sln -c Release`

For the spec-only change, targeted documentation verification is sufficient:
review this file and run `git diff --check`.

## Risks And Mitigations

The full matrix can produce roughly hundreds of screenshots. Keep it in a
dedicated category such as `VisualArtifacts` so it can be run directly when
reviewing responsive UI, and keep it out of the workflow's `fast` and `normal`
levels so routine CI does not become harder to triage.

Forced-colors support can be browser-version sensitive. Treat unsupported
browser context options as an explicit skipped variant with a reason in the
JSON index rather than silently omitting the coverage.

Dynamic or network-loaded images can make screenshots noisy. Stub external
portrait/image endpoints where the route already does not depend on testing the
image network path.

Tests that create pages outside the owned `E2ETestBase.Page` do not
automatically inherit base-class diagnostics. The visual-artifact helper should
attach diagnostics to every page it owns.
