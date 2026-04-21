// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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
public class HealthFunction(CosmosClient cosmos, IOptions<CosmosOptions> cosmosOpts, ILogger<HealthFunction> logger)
{
    [Function("health")]
    public IActionResult Live(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(Build());
    }

    /// <summary>
    /// Readiness probe — validates that this instance can talk to Cosmos.
    /// Response contract:
    ///   - 200 OK with <see cref="HealthResponse"/> (status="ready") on success.
    ///   - 503 Service Unavailable with anonymous-object body { status: "unready" } on failure.
    /// The exception is logged server-side (including type + message + stack) for
    /// operator triage. Nothing about the failure is exposed to the caller beyond
    /// the HTTP status code — clients must key off status, not a response string.
    /// </summary>
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
            logger.LogError(ex, "Readiness probe failed");
            return new ObjectResult(new { status = "unready" })
            {
                StatusCode = 503
            };
        }
    }

    internal static HealthResponse Build() => new("ok", DateTimeOffset.UtcNow);
}
