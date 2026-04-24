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
using Lfm.Api.Services;

namespace Lfm.Api.Functions;

public class MeDeleteFunction(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IIdempotencyStore idempotencyStore,
    ILogger<MeDeleteFunction> logger)
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

        // GDPR: purge the idempotency replay cache for this actor so the cache
        // cannot outlive the raider document it references. Best-effort — a
        // failure here is logged but does not block the account-delete.
        try
        {
            await idempotencyStore.PurgeForActorAsync(principal.BattleNetId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Idempotency purge failed for {BattleNetId}", principal.BattleNetId);
        }

        AuditLog.Emit(logger, new AuditEvent("account.delete", principal.BattleNetId, principal.BattleNetId, "success", null));

        // TS returns status 200 with { deleted: true }.
        return new OkObjectResult(new { deleted = true });
    }

    /// <summary>
    /// <c>/api/v1/me</c> DELETE alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("me-delete-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/me")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);
}
