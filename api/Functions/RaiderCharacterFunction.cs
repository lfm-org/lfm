// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Contracts.Raiders;

namespace Lfm.Api.Functions;

/// <summary>
/// Serves PUT /api/raider/characters/{id}.
///
/// Selects an already-cached character as the user's active character.
/// The character must already exist in the raider document's <c>characters</c> list
/// (populated by the POST /api/raider/character flow). If not found, returns 403.
///
/// Logic:
///   1. Load the raider document — 404 if not found.
///   2. Verify the character ID exists in the raider's stored characters list — 403 if not.
///   3. Update <c>selectedCharacterId</c> and upsert the document.
///   4. Return the updated selected character ID.
///
/// Mirrors the character-selection part of <c>handler</c> in
/// <c>functions/src/functions/raider-character.ts</c>.
/// </summary>
public class RaiderCharacterFunction(IRaidersRepository repo, ILogger<RaiderCharacterFunction> logger)
{
    [Function("raider-character")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "raider/characters/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal(); // non-null: [RequireAuth] + AuthPolicyMiddleware guarantee

        // 1. Load raider document.
        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        // 2. Verify character ownership — the character must already be stored in the
        //    raider document's characters list. Mirrors:
        //    if (!isCharacterOwnedByAccount(...)) return errorResponse(403, ...)
        var ownedCharacter = raider.Characters?.FirstOrDefault(c => c.Id == id);
        if (ownedCharacter is null)
            return Problem.Forbidden(
                req.HttpContext,
                "character-not-on-profile",
                "Character not found in your profile.");

        // 3. Update selectedCharacterId and persist.
        try
        {
            var updated = raider with { SelectedCharacterId = id };
            await repo.UpsertAsync(updated, ct);
        }
        catch (Exception ex)
        {
            return InternalErrorResult.Create(logger, ctx, ex, "raider-character select");
        }

        // 4. Return updated selection.
        return new OkObjectResult(new UpdateCharacterResponse(SelectedCharacterId: id));
    }

    /// <summary>
    /// <c>/api/v1/raider/characters/{id}</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("raider-character-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/raider/characters/{id}")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);
}
