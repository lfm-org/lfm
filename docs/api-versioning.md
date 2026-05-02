# API versioning

## What this is

The Lfm API canonicalised every route under `/api/v1/...` in Phase 7 of the
`review-api-precious-dewdrop` remediation plan. The pre-existing unprefixed
routes (`/api/runs`, `/api/me`, etc.) are kept as aliases for one release to
cover the SPA-deploy gap (older `wwwroot` bundles that haven't been re-published
yet still need to work).

## The alias pattern

Every HTTP function carries two `[Function]` declarations — one on the canonical
`v1/...` route and one on the legacy unprefixed route. Example:

```csharp
public class HealthFunction(CosmosClient cosmos, IOptions<CosmosOptions> cosmosOpts, ILogger<HealthFunction> logger)
{
    [Function("health")]
    public IActionResult Live(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
        => new OkObjectResult(Build());

    [Function("health-v1")]
    public IActionResult LiveV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/health")] HttpRequest req)
        => Live(req);
}
```

Rules:

1. The canonical method stays where it is; all existing logic lives there.
2. The v1 alias is a thin delegation — `=> Live(req)` — no logic duplication.
3. The `[Function]` id gets a `-v1` suffix so both declarations can coexist in
   the same host (`[Function]` ids must be unique).
4. New endpoints added from this point on land under `v1/` as their primary
   route and never get a legacy alias.

## Rollout staging

Phase 7 lands in family-per-PR increments so each branch stays reviewable:

| PR | Scope | Endpoints |
|---|---|---|
| 7.1a | Health family + this docs page | `/health`, `/health/ready` |
| 7.1b | Me / Guild / Privacy | `/me`, `/guild`, `/guild/admin`, `/privacy-contact/email` |
| 7.1c | Runs family | `/runs`, `/runs/{id}`, `/runs/{id}/signup`, `/admin/runs/migrate-schema` |
| 7.1d | Raider + BattleNet + WoW reference + Admin | `/raider/*`, `/battlenet/*`, `/wow/reference/*` |
| 7.1e | SPA flip + openapi servers bump + README | Consumer cutover |

Each PR is small and does not break backwards compatibility. The SPA flip in
7.1e is the coordination point where the client starts issuing `/api/v1/...`
requests; after one full release cycle the legacy routes can be removed in a
follow-up PR (not part of this plan — tracked as technical debt at that point).

## When to retain the alias

The alias is a **migration aid**, not a long-term contract. Remove the legacy
route once:

- The SPA build has been on `/api/v1/...` for at least one deploy cycle.
- App Insights traffic on the unprefixed routes is effectively zero (check
  `requests | where url !startswith "/api/v1/"` over the last 7 days).
- Any documented consumer (the OpenAPI contract at `api/openapi.yaml`) has
  been updated to list only `/api/v1/` as the server.

Until all three are true, keep the alias in place.

## Alias retirement tracker

The compatibility window started on **2026-04-25** with commit `460bf65`
(`Flip SPA, openapi, and docs to /api/v1/ as the canonical path`). The current
machine-readable state lives in
[`api-alias-retirement.json`](api-alias-retirement.json). Keep
`legacyAliasesAllowed` set to `true` until the removal criteria above are met.
When they are met, set it to `false` in the same PR that removes the remaining
unprefixed production routes and run:

```bash
scripts/check-api-alias-retirement.sh
```

The check intentionally fails if `legacyAliasesAllowed=false` while any
unprefixed production `[HttpTrigger(... Route = "...")]` declarations remain.
It ignores the test-only `e2e/` route and the CORS catch-all route because they
are not legacy public API aliases.

### Telemetry query

Use Application Insights over the final 7-day observation window:

```kusto
requests
| where timestamp >= ago(7d)
| extend path = tostring(parse_url(url).Path)
| where path startswith "/api/"
| where path !startswith "/api/v1/"
| where path !startswith "/api/e2e/"
| where path != "/api/{*path}"
| summarize requestCount = count() by operation_Name, path
| order by requestCount desc
```

The removal PR should include the query window, result summary, and the
production deployment marker that proves at least one deploy cycle has passed
since the SPA cutover.

### Current live legacy aliases

These are transitional live interfaces until the tracker is flipped and the
routes are removed:

- Health: `GET /api/health`, `GET /api/health/ready`.
- Me: `GET /api/me`, `PATCH /api/me`, `DELETE /api/me`.
- Guild: `GET /api/guild`, `PATCH /api/guild`, `GET /api/guild/admin`.
- Runs: `GET /api/runs`, `POST /api/runs`, `GET /api/runs/{id}`,
  `PUT /api/runs/{id}`, `DELETE /api/runs/{id}`,
  `POST /api/runs/{id}/signup`, `DELETE /api/runs/{id}/signup`,
  `GET /api/runs/{id}/signup/options`,
  `POST /api/admin/runs/migrate-schema`.
- Raider characters: `POST /api/raider/character`,
  `PUT /api/raider/characters/{id}`,
  `POST /api/raider/characters/{id}/enrich`.
- Battle.net: `GET /api/battlenet/login`,
  `GET /api/battlenet/callback`, `GET /api/battlenet/logout`,
  `GET /api/battlenet/characters`,
  `POST /api/battlenet/characters/refresh`,
  `POST /api/battlenet/character-portraits`.
- WoW reference: `GET /api/wow/reference/expansions`,
  `GET /api/wow/reference/instances`,
  `GET /api/wow/reference/specializations`,
  `POST /api/wow/reference/refresh`.
- Privacy: `GET /api/privacy-contact/email`.

## Why alias-by-second-function rather than middleware rewrite

The Azure Functions isolated worker dispatches routes at the host layer
(before our `IFunctionsWorkerMiddleware` runs). Rewriting `/api/x` to
`/api/v1/x` from the worker middleware would fire *after* route matching,
too late to help. Declaring both routes in parallel is the idiomatic
approach and keeps the route table explicit — both routes show up in the
Functions host's startup log and in any `az functionapp function list` dump.
