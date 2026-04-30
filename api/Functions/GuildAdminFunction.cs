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

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/guild/admin?guildId={id}.
///
/// Returns the guild document for any guild by ID.
/// Restricted to site administrators.
/// </summary>
public class GuildAdminFunction(IGuildRepository guildRepo, ISiteAdminService siteAdmin)
{
    [Function("guild-admin-get")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "guild/admin")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        if (!await siteAdmin.IsAdminAsync(principal.BattleNetId, cancellationToken))
            return Problem.Forbidden(req.HttpContext, "admin-only", "Site administrator access required.");

        var guildId = req.Query["guildId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(guildId))
            return Problem.BadRequest(req.HttpContext, "missing-parameter", "guildId query parameter is required.");

        var guildDoc = await guildRepo.GetAsync(guildId, cancellationToken);
        if (guildDoc is null)
            return Problem.NotFound(req.HttpContext, "guild-not-found", "Guild not found.");

        return new OkObjectResult(GuildMapper.MapToDto(guildDoc, GuildEffectivePermissions.SiteAdminView));
    }

    /// <summary>
    /// <c>/api/v1/guild/admin</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("guild-admin-get-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/guild/admin")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);
}
