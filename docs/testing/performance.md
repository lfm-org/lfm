# Performance Testing

LFM treats performance as several cheap regression signals plus production
telemetry. Local and CI checks should catch obvious regressions before merge;
Application Insights remains the operational production signal.

## Lanes

| Lane | Runner | Cadence | Gate |
|------|--------|---------|------|
| Bundle size | `scripts/check-bundle-size.sh` after `dotnet publish app/Lfm.App.csproj -c Release` | Every CI and deploy-app build | Hard fail over 5 MB brotli; warning over 10% growth from baseline |
| Browser journey timing | `dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter "Category=Performance"` | Manual dispatch and local investigation | Hybrid gate: hard fail on browser/network errors and p75 local poor-threshold regressions; other timing drift advisory |
| Local load smoke | `dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter "Category=PerformanceLoad"` | Manual dispatch and local investigation | Hard fail only when bounded local-stack requests exceed the explicit error threshold; timing percentiles advisory |
| Scheduled synthetic | `.github/workflows/performance-synthetic.yml` | Daily at 03:17 UTC and manual dispatch | Visible workflow status; not wired into PR, deploy, or release blocking |
| Backend latency | API tests plus Application Insights queries below | Unit/API tests on PR; production queries during operations | Operation-count tests are hard gates; production percentiles are operational evidence |
| Manual load/investigation | Temporary local harnesses documented here before use | Only when a regression needs diagnosis | Advisory; no always-on paid load service without an explicit cost note |

Every-PR gates must stay cheap for this hobby/free-tier project. Do not add
always-on hosted load tests or paid monitoring dependencies without discussing
the recurring cost first.

## Current Budgets

| Signal | Initial budget | Meaning |
|--------|----------------|---------|
| Brotli bundle total | 5 MB hard budget | Regression ceiling, not a target |
| Bundle growth | Warn above 10% over `docs/testing/bundle-size-baseline.json` | Review prompt while under the hard budget |
| Cold public landing load | 60 seconds | Local-stack browser guard |
| Authenticated `/runs` load | 60 seconds | Local-stack browser guard |
| `/runs/new` form load | 45 seconds | Local-stack browser guard |
| `/characters` list load | 45 seconds | Local-stack browser guard |
| Warm route navigation | 20 seconds | Local-stack browser guard |
| Local LCP p75 | 4000 ms poor threshold | Hard browser regression gate when supported |
| Local CLS p75 | 0.25 poor threshold | Hard browser regression gate when supported |
| Controlled interaction p75 | 500 ms poor-threshold proxy | Hard local lab gate for measured route/form interactions |
| Backend p95/p99 | Placeholder until production baseline exists | Use the KQL below to establish normal ranges |

Browser journeys run in desktop and mobile viewport profiles and collect two
samples per journey by default; scheduled synthetic collection sets
`LFM_E2E_PERFORMANCE_SAMPLES=4` so p75 is meaningful enough for trend evidence.
Override locally with `LFM_E2E_PERFORMANCE_SAMPLES` when investigating a
regression. The elapsed-time budgets intentionally allow slow CI machines and
first-run WASM startup. Tighten them only after several clean baseline runs.

The browser report is schema v2 at
`artifacts/e2e-results/performance-report.json`. It records cache state, user
state, browser and runner metadata, p50/p75/max elapsed timing, Web
Vitals-style lab metrics, API resource timing summaries, support flags, raw
samples, gate policy, and threshold source. LCP/CLS thresholds are based on
web.dev Core Web Vitals poor thresholds. Controlled interaction duration is a
local lab proxy for route/form responsiveness, not a claim of production INP.

`expired-session-protected-route-redirect` measures the user-visible path when
a browser session has expired and a protected route must redirect to `/login`.
The journey intentionally allows the `/api/v1/me` 401 that proves the session is
expired; other 4xx/5xx responses remain diagnostic failures.

## Local Load Smoke

The `PerformanceLoad` E2E lane is a smoke probe for local-stack request health,
not a capacity test and not a production SLO. It uses the seeded E2E stack only:
local app host, local API container, Cosmos emulator, Azurite, and test-mode
auth. It must not call real Battle.net or any hosted load provider.

The lane keeps load bounded by code constants: low concurrency, a fixed request
count per probe, a total request limit, a per-request timeout, and a per-probe
timeout. Its JSON report is written to
`artifacts/e2e-results/performance-load-report.json` and records each tested
endpoint/journey with request count, failure count, p50, p95, max, expected
status codes, endpoint group, p50, p75, p95, max, and raw samples. Timing
percentiles are evidence for comparison; the gate fails only when request
errors or unexpected statuses exceed the explicit threshold.

## Scheduled Synthetic Collection

`Performance Synthetic` runs once per day at 03:17 UTC and can also be started
manually from GitHub Actions. It runs the local-stack `Performance` and
`PerformanceLoad` E2E categories in one test invocation, then performs three
anonymous production GET checks: the public frontend root, `/api/health`, and
`/api/v1/health`.

The workflow uploads `performance-synthetic-reports` for 30 days. The artifact
contains the browser timing report, load-smoke report, TRX result, and
production synthetic report when those files are available. Daily runner cost is
capped by a 45-minute timeout and is expected to stay in the same range as a
manual performance E2E run; production checks add three samples per anonymous
probe with a 10-second per-request timeout. The workflow also writes a compact
GitHub step summary from the browser, load-smoke, and production synthetic
reports.

Scheduled synthetic failures are intentionally visible in their own workflow
status but initially non-blocking for deploys, releases, and PR smoke E2E. Do
not add authenticated production flows or managed test credentials here without
a separate approved issue.

## Evidence Rules

- Bundle-size output is a hard build signal. The report should show total bytes,
  top assets, baseline total, and growth percentage on every run.
- Browser performance E2E is a regression signal. Its versioned JSON report
  records browser metadata, runner metadata, viewport profile, cache/user state,
  p50/p75/max timing, Web Vitals-style lab metrics, resource/API timing
  summaries, raw samples, request failures, unexpected HTTP 4xx/5xx responses,
  and console errors. It is not a production SLO.
- Browser performance E2E fails on unexpected request failures, unexpected HTTP
  4xx/5xx responses, console errors, and supported p75 local poor-threshold
  regressions unless the spec has a narrow commented allowlist for the expected
  case.
- Local load smoke E2E is a request-health probe. Its versioned JSON report
  records bounded request counts, failure counts, p50, p75, p95, max, tested
  endpoint groups and journeys, expected status codes, and raw samples. It is
  not a capacity test and must not be used as a production SLO.
- Scheduled synthetic production checks are anonymous availability probes only.
  They collect a small multi-sample JSON report with advisory p50/p75/max timing
  and must stay low-volume, unauthenticated, and outside PR/deploy blocking until
  a separate issue promotes the signal.
- Backend elapsed-ms logs and dependency telemetry are operational evidence.
  They are not a full load test and should be read as percentiles, not anecdotes.
- Bundle optimization belongs in #27 after approved production evidence or
  scheduled synthetic baselines establish the problem shape.

## Feature Ownership

Update performance coverage when a change adds a new user-critical journey,
materially changes the Blazor boot path, adds large static assets, changes a hot
API path, adds Cosmos queries, or changes reference-data lookup behavior.
Prefer deterministic operation-count assertions for backend PR gates. Use
wall-clock checks only where the browser is the behavior being protected.

## Lab vs Production

Local Playwright metrics are lab data: useful for catching regressions in the
same harness, but not a replacement for field data. The local E2E stack uses
published Blazor assets, a local Kestrel static host, an API container, Cosmos
emulator, Azurite, and synthetic seed data. Production uses Static Web Apps,
Azure Functions, real network paths, managed storage, live caches, and real user
devices. Compare local runs against local baselines and production synthetics
against production history; do not compare the raw numbers as if the
environments were equivalent.

## Bundle Baseline Updates

Regenerate the app publish output, run the bundle check, inspect the report, and
commit `docs/testing/bundle-size-baseline.json` only when the growth is
intentional.

```bash
dotnet publish app/Lfm.App.csproj -c Release -o ./publish/app
BUNDLE_UPDATE_BASELINE=1 ./scripts/check-bundle-size.sh ./publish/app/wwwroot 5
```

## Production Queries

Run these in the Application Insights or Log Analytics workspace connected to
the production Function App. Keep alerting conservative until the normal traffic
shape is known.

API latency percentiles by function:

```kusto
traces
| where timestamp > ago(24h)
| where customDimensions.FunctionName != ""
| extend elapsedMs = todouble(customDimensions.ElapsedMs)
| where isnotnull(elapsedMs)
| summarize count(), p50=percentile(elapsedMs, 50), p95=percentile(elapsedMs, 95), p99=percentile(elapsedMs, 99) by tostring(customDimensions.FunctionName)
| order by p95 desc
```

Dependency duration and failures:

```kusto
dependencies
| where timestamp > ago(24h)
| summarize count(), failures=countif(success == false), p95=percentile(duration, 95), p99=percentile(duration, 99) by type, target, name
| order by p95 desc
```

HTTP 429 and Retry-After signals:

```kusto
requests
| where timestamp > ago(24h)
| where resultCode == "429"
| summarize count() by name, bin(timestamp, 15m)
| order by timestamp desc
```

## Alert Boundaries

Page or block release only for sustained production-impacting signals: repeated
function failures, elevated 429s, dependency failures, or p95/p99 latency that is
both sustained and tied to user-visible errors. Treat isolated slow samples,
manual E2E timing drift, and bundle growth warnings as advisory review signals
unless they cross an existing hard gate.
