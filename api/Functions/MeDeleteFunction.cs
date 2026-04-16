// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

public class MeDeleteFunction(IRunsRepository runsRepo, IRaidersRepository raidersRepo, ILogger<MeDeleteFunction> logger)
{
    [Function("me-delete")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // Mirror TS order: scrub raider from runs FIRST, then delete the raider document.
        // This ensures no run documents are left referencing a deleted raider.
        await runsRepo.ScrubRaiderAsync(principal.BattleNetId, cancellationToken);
        await raidersRepo.DeleteAsync(principal.BattleNetId, cancellationToken);

        AuditLog.Emit(logger, new AuditEvent("account.delete", principal.BattleNetId, principal.BattleNetId, "success", null));

        // TS returns status 200 with { deleted: true }.
        return new OkObjectResult(new { deleted = true });
    }
}
