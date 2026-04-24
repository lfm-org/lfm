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

## Why alias-by-second-function rather than middleware rewrite

The Azure Functions isolated worker dispatches routes at the host layer
(before our `IFunctionsWorkerMiddleware` runs). Rewriting `/api/x` to
`/api/v1/x` from the worker middleware would fire *after* route matching,
too late to help. Declaring both routes in parallel is the idiomatic
approach and keeps the route table explicit — both routes show up in the
Functions host's startup log and in any `az functionapp function list` dump.
