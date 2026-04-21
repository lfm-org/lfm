// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Services;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves POST /api/wow/reference/refresh (admin only).
///
/// Syncs WoW reference data (instances, specializations) from the Blizzard
/// Game Data API into the blob reference store.
///
/// Auth:
///   - [RequireAuth] → AuthPolicyMiddleware returns 401 for unauthenticated callers.
///   - ISiteAdminService check → 403 for authenticated non-admin callers.
///
/// Response: { results: [{ name, status }] } — one entry per entity type,
/// reporting "synced (N docs)" or "failed: ..." per entity.
/// </summary>
public class WowReferenceRefreshFunction(ISiteAdminService siteAdmin, IReferenceSync referenceSync)
{
    [Function("wow-reference-refresh")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "wow/reference/refresh")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        if (!await siteAdmin.IsAdminAsync(principal.BattleNetId, ct))
            return new ObjectResult(new { error = "Forbidden" }) { StatusCode = 403 };

        var response = await referenceSync.SyncAllAsync(ct);
        return new OkObjectResult(response);
    }
}
