// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Me;

namespace Lfm.Api.Functions;

public class MeFunction(IRaidersRepository repo, ISiteAdminService siteAdmin)
{
    [Function("me")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var isAdmin = await siteAdmin.IsAdminAsync(principal.BattleNetId, cancellationToken);

        var (_, guildName) = GuildResolver.FromRaider(raider);

        // Emit the Cosmos _etag so the SPA can echo it as If-Match on PATCH /me
        // for optimistic concurrency. Cosmos etags are already double-quoted
        // opaque strings and can be mirrored verbatim.
        if (!string.IsNullOrEmpty(raider.ETag))
            req.HttpContext.Response.Headers.ETag = raider.ETag;

        return new OkObjectResult(new MeResponse(
            BattleNetId: principal.BattleNetId,
            GuildName: guildName,
            SelectedCharacterId: raider.SelectedCharacterId,
            IsSiteAdmin: isAdmin,
            Locale: raider.Locale));
    }

    /// <summary>
    /// <c>/api/v1/me</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c> for the rollout plan.
    /// </summary>
    [Function("me-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);
}
