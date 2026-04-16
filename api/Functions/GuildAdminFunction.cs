// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
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
            return new ObjectResult(new { error = "Forbidden" }) { StatusCode = 403 };

        var guildId = req.Query["guildId"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(guildId))
            return new BadRequestObjectResult(new { error = "guildId query parameter is required" });

        var guildDoc = await guildRepo.GetAsync(guildId, cancellationToken);
        if (guildDoc is null)
            return new NotFoundResult();

        return new OkObjectResult(GuildMapper.MapToDto(guildDoc));
    }
}
