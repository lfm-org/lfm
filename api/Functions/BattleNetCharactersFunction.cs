// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// GET /api/battlenet/characters — returns the logged-in user's WoW characters.
///
/// Serves from the cached <c>accountProfileSummary</c> in the raider document when
/// the 15-minute cooldown has not yet expired.  Returns 204 when no cached data
/// exists or the cooldown has expired, signalling that the caller should POST to
/// /api/battlenet/characters/refresh (B2.5) to populate or refresh the cache.
///
/// This mirrors the TypeScript handler in
/// <c>functions/src/functions/battlenet-characters.ts</c>.
/// </summary>
public class BattleNetCharactersFunction(
    IRaidersRepository repo,
    IOptions<BlizzardOptions> blizzardOptions)
{
    private const int AccountCharsCooldownMs = 15 * 60 * 1000; // mirrors ACCOUNT_CHARS_COOLDOWN_MS

    private readonly BlizzardOptions _blizzardOpts = blizzardOptions.Value;

    [Function("battlenet-characters")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "battlenet/characters")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal();

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        if (!ShouldServeCachedAccountProfile(raider))
            return new NoContentResult();

        var region = _blizzardOpts.Region.ToLowerInvariant();
        var characters = AccountCharacterMapper.MapToCharacterDtos(
            raider.AccountProfileSummary!,
            region,
            raider.Characters,
            raider.PortraitCache);

        req.HttpContext.Response.Headers["Cache-Control"] = "private, max-age=300";
        return new OkObjectResult(characters);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/characters</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-characters-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/battlenet/characters")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);

    // ---------------------------------------------------------------------------
    // Internal helpers — internal for unit-test access
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns true when a cached account profile summary exists and is still within
    /// the 15-minute cooldown window.  Mirrors <c>shouldServeCachedAccountProfile</c>
    /// from the TypeScript source (test-mode path omitted — .NET uses real auth).
    /// </summary>
    internal static bool ShouldServeCachedAccountProfile(RaiderDocument raider)
    {
        if (raider.AccountProfileSummary is null) return false;
        if (raider.AccountProfileRefreshedAt is null) return false;

        var elapsed = DateTimeOffset.UtcNow - DateTimeOffset.Parse(raider.AccountProfileRefreshedAt);
        return elapsed.TotalMilliseconds < AccountCharsCooldownMs;
    }
}
