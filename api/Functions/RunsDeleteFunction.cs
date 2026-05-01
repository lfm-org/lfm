// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves DELETE /api/runs/{id}.
///
/// Deletes a run document from Cosmos. The run's embedded runCharacters are
/// removed implicitly — deleting the document clears all signups.
///
/// Permission rules (mirrors runs-delete.ts):
///   - The creator can always delete their own run.
///   - A non-creator can delete a run if they belong to the same guild and
///     hold the <c>canDeleteGuildRuns</c> rank permission.
///   - All other callers receive 403.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-delete.ts</c>.
/// </summary>
public class RunsDeleteFunction(IRunsRepository repo, IRaidersRepository raidersRepo, IGuildPermissions guildPermissions, ILogger<RunsDeleteFunction> logger)
{
    [Function("runs-delete")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // 1. Load existing run.
        var existing = await repo.GetByIdAsync(id, ct);
        if (existing is null)
            return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

        // 2. Permission check — mirrors runs-delete.ts:
        //    Creator can always delete. Non-creator must be in the same guild with
        //    canDeleteGuildRuns permission.
        var isCreator = RunAccessPolicy.IsCreator(existing, principal.BattleNetId);
        if (!isCreator)
        {
            // Load the raider and derive guild info from the selected character.
            // principal.GuildId is a legacy session field and is no longer populated.
            var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
            if (raider is null)
                return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

            var (guildId, _) = GuildResolver.FromRaider(raider);

            if (!RunAccessPolicy.IsGuildPeer(existing, principal.BattleNetId, guildId))
            {
                AuditLog.Emit(logger, new AuditEvent("run.delete", principal.BattleNetId, id, "failure", "not creator"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "run-delete-not-creator",
                    "Only the run creator can delete this run.");
            }

            var canDelete = await guildPermissions.CanDeleteGuildRunsAsync(raider, ct);
            if (!canDelete)
            {
                AuditLog.Emit(logger, new AuditEvent("run.delete", principal.BattleNetId, id, "failure", "guild rank denied"));
                return Problem.Forbidden(
                    req.HttpContext,
                    "guild-rank-denied",
                    "Your guild rank does not have permission to delete guild runs.");
            }
        }

        // 3. Delete the run document. Signups are embedded — they are removed with the document.
        await repo.DeleteAsync(id, ct);

        AuditLog.Emit(logger, new AuditEvent("run.delete", principal.BattleNetId, id, "success", null));

        // TS returns status 200 with { deleted: true }.
        return new OkObjectResult(new { deleted = true });
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}</c> DELETE alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-delete-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);
}
