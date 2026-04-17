// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

public class SpecializationsListFunction(ISpecializationsRepository repo)
{
    [Function("specializations-list")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "reference/specializations")] HttpRequest req,
        CancellationToken ct)
    {
        var items = await repo.ListAsync(ct);
        return new OkObjectResult(items);
    }
}
