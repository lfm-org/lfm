// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves DELETE /api/runs/{id}/signup.
///
/// Lets an authenticated user cancel their signup for a run by removing their
/// <see cref="RunCharacterEntry"/> from the run document's <c>runCharacters</c> array.
///
/// Logic:
///   1. Load the run — 404 if not found.
///   2. Check guild-only visibility access.
///   3. Find the user's entry in runCharacters by raiderBattleNetId.
///   4. 404 if the user is not signed up.
///   5. Remove the entry from the array.
///   6. Persist the run document.
///   7. Return the sanitized run.
///
/// Mirrors <c>handler</c> in <c>functions/src/functions/runs-cancel-signup.ts</c>.
/// </summary>
public class RunsCancelSignupFunction(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    ILogger<RunsCancelSignupFunction> logger)
{
    [Function("runs-cancel-signup")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "runs/{id}/signup")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // 1. Load existing run.
        var run = await runsRepo.GetByIdAsync(id, ct);
        if (run is null)
            return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

        // 2. Derive the caller's guild from the raider's selected character.
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        var (guildId, _) = GuildResolver.FromRaider(raider);

        if (!RunAccessPolicy.CanView(run, principal.BattleNetId, guildId))
            return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

        // 3. Find the user's entry in runCharacters by raiderBattleNetId.
        var existingIndex = -1;
        for (var i = 0; i < run.RunCharacters.Count; i++)
        {
            if (run.RunCharacters[i].RaiderBattleNetId == principal.BattleNetId)
            {
                existingIndex = i;
                break;
            }
        }

        // 4. Return 404 if the user is not signed up.
        if (existingIndex < 0)
            return Problem.NotFound(req.HttpContext, "signup-not-found", "No signup found.");

        // 5. Remove the entry from the array.
        var updatedCharacters = run.RunCharacters.ToList();
        updatedCharacters.RemoveAt(existingIndex);

        // 6. Persist the updated run document. ifMatchEtag is null here — cancel
        //    signup is not a client-driven If-Match flow; the repository falls back
        //    to run.ETag for concurrency.
        var updated = run with { RunCharacters = updatedCharacters };
        var persisted = await runsRepo.UpdateAsync(updated, ifMatchEtag: null, ct);

        AuditLog.Emit(logger, new AuditEvent("signup.cancel", principal.BattleNetId, id, "success", null));

        // 7. Return sanitized run — mirrors sanitizeRunDocumentForResponse.
        return new OkObjectResult(RunResponseMapper.ToDetail(persisted, principal.BattleNetId));
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}/signup</c> DELETE alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-cancel-signup-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/runs/{id}/signup")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);

}
