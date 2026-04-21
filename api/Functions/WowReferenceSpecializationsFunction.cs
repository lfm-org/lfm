// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

public class WowReferenceSpecializationsFunction(ISpecializationsRepository repo)
{
    [Function("wow-reference-specializations")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wow/reference/specializations")] HttpRequest req,
        CancellationToken ct)
    {
        var items = await repo.ListAsync(ct);
        return new OkObjectResult(items);
    }
}
