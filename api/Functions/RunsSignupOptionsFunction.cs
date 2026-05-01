// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Runs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

public class RunsSignupOptionsFunction(IRunSignupOptionsService service)
{
    [Function("runs-signup-options")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs/{id}/signup/options")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal();
        var result = await service.GetAsync(id, principal, ct);

        return result switch
        {
            RunSignupOptionsResult.Ok ok => new OkObjectResult(ok.Options),
            RunSignupOptionsResult.NeedsRefresh => new NoContentResult(),
            RunSignupOptionsResult.NotFound nf => Problem.NotFound(req.HttpContext, nf.Code, nf.Message),
            RunSignupOptionsResult.Forbidden fb => Problem.Forbidden(req.HttpContext, fb.Code, fb.Message),
            _ => Problem.InternalError(req.HttpContext, "unexpected-result", "Unexpected signup options result."),
        };
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}/signup/options</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-signup-options-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/runs/{id}/signup/options")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);
}
