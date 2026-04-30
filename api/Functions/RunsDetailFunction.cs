// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves GET /api/runs/{id}.
///
/// Returns a single run by its Cosmos document id, sanitized so that each
/// RunCharacter's raiderBattleNetId is replaced by an IsCurrentUser flag.
///
/// Visibility rules (mirrors runs-detail.ts):
///   - The run is always returned if it is PUBLIC.
///   - The run is returned for GUILD visibility when:
///       * the requesting user is the creator, OR
///       * the requesting user belongs to the same guild as the creator.
///   - All other cases return 404 (no information leakage).
/// </summary>
public class RunsDetailFunction(IRunsRepository repo, IRaidersRepository raidersRepo)
{
    [Function("runs-detail")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        var run = await repo.GetByIdAsync(id, ct);
        if (run is null)
            return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");

        // Visibility check — mirrors runs-detail.ts:
        //   if (resource.visibility === "GUILD" && !isCreator && !isGuildMember)
        //     return errorResponse(404, "Run not found");
        if (run.Visibility == "GUILD")
        {
            // Derive the caller's guild from their raider's selected character so
            // that guild membership is always taken from the stored character data
            // (principal.GuildId is a legacy session field that is no longer populated).
            var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
            if (raider is null)
                return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

            var (guildId, _) = GuildResolver.FromRaider(raider);

            if (!RunAccessPolicy.CanView(run, principal.BattleNetId, guildId))
                return Problem.NotFound(req.HttpContext, "run-not-found", "Run not found.");
        }

        // Emit the Cosmos _etag as a strong HTTP ETag so callers can send it back
        // on PUT /api/runs/{id} as an If-Match header for optimistic concurrency.
        // The Cosmos _etag is already a double-quoted opaque string, e.g.
        // "\"abc123\"" — use it verbatim.
        if (!string.IsNullOrEmpty(run.ETag))
            req.HttpContext.Response.Headers.ETag = run.ETag;

        return new OkObjectResult(RunResponseMapper.ToDetail(run, principal.BattleNetId));
    }

    /// <summary>
    /// <c>/api/v1/runs/{id}</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("runs-detail-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/runs/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);

}
