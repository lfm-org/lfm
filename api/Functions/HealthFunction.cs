using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;
using Lfm.Contracts.Health;

namespace Lfm.Api.Functions;

// WAF/Reliability: two endpoints, deliberately separated.
//   /api/health       — liveness. Static "ok". Never touches downstream deps.
//                       App Service Health Check (when on Premium/Dedicated) pings this
//                       to decide whether to recycle the instance. Returning 503 here
//                       because Cosmos is down would take the entire app offline.
//   /api/health/ready — readiness. Validates Cosmos connectivity. Consumed by external
//                       monitors / deploy smoke tests, not by App Service Health Check.
public class HealthFunction(CosmosClient cosmos, IOptions<CosmosOptions> cosmosOpts)
{
    [Function("health")]
    public IActionResult Live(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(Build());
    }

    [Function("health-ready")]
    public async Task<IActionResult> Ready(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health/ready")] HttpRequest req,
        CancellationToken ct)
    {
        try
        {
            // Cheapest possible round-trip to validate credential + network + account.
            await cosmos.GetDatabase(cosmosOpts.Value.DatabaseName).ReadAsync(cancellationToken: ct);
            return new OkObjectResult(new HealthResponse("ready", DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { status = "unready", error = ex.GetType().Name })
            {
                StatusCode = 503
            };
        }
    }

    internal static HealthResponse Build() => new("ok", DateTimeOffset.UtcNow);
}
