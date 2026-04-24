// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

public class WowReferenceExpansionsFunction(IExpansionsRepository repo)
{
    [Function("wow-reference-expansions")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wow/reference/expansions")] HttpRequest req,
        CancellationToken ct)
    {
        var items = await repo.ListAsync(ct);
        return new OkObjectResult(items);
    }

    /// <summary>
    /// <c>/api/v1/wow/reference/expansions</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("wow-reference-expansions-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/wow/reference/expansions")] HttpRequest req,
        CancellationToken ct)
        => Run(req, ct);
}
