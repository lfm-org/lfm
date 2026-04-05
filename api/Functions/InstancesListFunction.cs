using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

public class InstancesListFunction(IInstancesRepository repo)
{
    [Function("instances-list")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "instances")] HttpRequest req,
        CancellationToken ct)
    {
        var items = await repo.ListAsync(ct);
        return new OkObjectResult(items);
    }
}
