# Performance Testing

LFM treats performance as several cheap regression signals plus production
telemetry. Local and CI checks should catch obvious regressions before merge;
Application Insights and future Web Vitals/RUM data remain the product truth.

## Lanes

| Lane | Runner | Cadence | Gate |
|------|--------|---------|------|
| Bundle size | `scripts/check-bundle-size.sh` after `dotnet publish app/Lfm.App.csproj -c Release` | Every CI and deploy-app build | Hard fail over 5 MB brotli; warning over 10% growth from baseline |
| Browser journey timing | `dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter "Category=Performance"` | Manual dispatch and local investigation | Advisory regression guard with loose budgets and JSON artifact |
| RUM/Core Web Vitals | Issue #189 | Production once instrumented | Product truth; review percentile trends before optimization work |
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
| Backend p95/p99 | Placeholder until production baseline exists | Use the KQL below to establish normal ranges |

The browser budgets intentionally allow slow CI machines and first-run WASM
startup. Tighten them only after several clean baseline runs.

## Evidence Rules

- Bundle-size output is a hard build signal. The report should show total bytes,
  top assets, baseline total, and growth percentage on every run.
- Browser performance E2E is a regression signal. It records timing artifacts,
  request failures, and loose budget results, but it is not a production SLO.
- RUM/Core Web Vitals from #189 will be the source of client-side product truth.
- Backend elapsed-ms logs and dependency telemetry are operational evidence.
  They are not a full load test and should be read as percentiles, not anecdotes.
- Bundle optimization belongs in #27 after RUM establishes a real-user baseline.

## Feature Ownership

Update performance coverage when a change adds a new user-critical journey,
materially changes the Blazor boot path, adds large static assets, changes a hot
API path, adds Cosmos queries, or changes reference-data lookup behavior.
Prefer deterministic operation-count assertions for backend PR gates. Use
wall-clock checks only where the browser is the behavior being protected.

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

Web Vitals percentiles after #189:

```kusto
customMetrics
| where timestamp > ago(24h)
| where name in ("webvital_lcp", "webvital_inp", "webvital_cls")
| summarize p50=percentile(value, 50), p75=percentile(value, 75), p95=percentile(value, 95) by name, bin(timestamp, 1h)
| order by timestamp desc
```

## Alert Boundaries

Page or block release only for sustained production-impacting signals: repeated
function failures, elevated 429s, dependency failures, or p95/p99 latency that is
both sustained and tied to user-visible errors. Treat isolated slow samples,
manual E2E timing drift, and bundle growth warnings as advisory review signals
unless they cross an existing hard gate.
