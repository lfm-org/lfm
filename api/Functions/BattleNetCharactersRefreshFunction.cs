// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Mappers;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Services.Blizzard;
using Lfm.Api.Services.Blizzard.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Functions;

/// <summary>
/// POST /api/battlenet/characters/refresh — fetches a fresh WoW account profile
/// summary from Blizzard, stores it on the raider document, and returns the
/// mapped character list.
///
/// Rate-limited: if the profile was refreshed less than 15 minutes ago,
/// returns 429 with a <c>Retry-After</c> header (seconds until the cooldown
/// expires).  On Blizzard API failure the cooldown is NOT updated, so a failed
/// attempt does not consume the quota.
///
/// The Battle.net access token is read from the session principal stored in the
/// auth cookie (populated by the OAuth callback, B2.2).
///
/// This mirrors the TypeScript handler in
/// <c>functions/src/functions/battlenet-characters-refresh.ts</c>.
/// </summary>
public class BattleNetCharactersRefreshFunction(
    IRaidersRepository repo,
    IBlizzardProfileClient profileClient,
    IOptions<BlizzardOptions> blizzardOptions)
{
    // 15 minutes in milliseconds — mirrors ACCOUNT_CHARS_COOLDOWN_MS in cache.ts.
    private const int AccountCharsCooldownMs = 15 * 60 * 1000;

    private readonly BlizzardOptions _blizzardOpts = blizzardOptions.Value;

    [Function("battlenet-characters-refresh")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "battlenet/characters/refresh")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
    {
        var principal = ctx.GetPrincipal();

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, cancellationToken);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        // Rate-limit check: mirror cooldownRemaining() from cache.ts.
        var remainingSeconds = CooldownRemainingSeconds(raider.AccountProfileRefreshedAt, AccountCharsCooldownMs);
        if (remainingSeconds > 0)
        {
            return Problem.TooManyRequests(
                req.HttpContext,
                "characters-refresh-cooldown",
                "Too many requests.",
                retryAfterSeconds: remainingSeconds);
        }

        // Access token is stored in the session principal (populated at OAuth callback time).
        // If missing (old session before B2.5), we cannot call Blizzard.
        var accessToken = principal.AccessToken;
        if (string.IsNullOrEmpty(accessToken))
            return Problem.Unauthorized(
                req.HttpContext,
                "missing-access-token",
                "Session does not contain an access token. Please log out and log in again.");

        AccountProfileSummaryResponse freshSummary;
        try
        {
            freshSummary = await profileClient.GetAccountProfileSummaryAsync(accessToken, cancellationToken);
        }
        catch
        {
            // Do not update accountProfileRefreshedAt — a failed attempt must not consume the cooldown.
            return Problem.UpstreamFailed(
                req.HttpContext,
                "blizzard-upstream-failed",
                "Failed to fetch characters from Blizzard.");
        }

        var freshSummaryStored = BlizzardModelTranslator.ToStored(freshSummary);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var updated = raider with
        {
            AccountProfileSummary = freshSummaryStored,
            AccountProfileFetchedAt = now,
            AccountProfileRefreshedAt = now,
            Ttl = 180 * 86400,
        };
        await repo.UpsertAsync(updated, cancellationToken);

        var region = _blizzardOpts.Region.ToLowerInvariant();
        var characters = AccountCharacterMapper.MapToCharacterDtos(
            freshSummaryStored, region, raider.Characters, raider.PortraitCache);

        return new OkObjectResult(characters);
    }

    /// <summary>
    /// <c>/api/v1/battlenet/characters/refresh</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("battlenet-characters-refresh-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/battlenet/characters/refresh")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken cancellationToken)
        => Run(req, ctx, cancellationToken);

    // ---------------------------------------------------------------------------
    // Internal helpers — internal for unit-test access
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns the number of whole seconds remaining in the cooldown window, or
    /// zero when the cooldown has expired or no timestamp is present.
    ///
    /// Mirrors <c>cooldownRemaining(timestamp, ttlMs)</c> in
    /// <c>functions/src/lib/cache.ts</c>:
    ///   Math.max(0, Math.ceil((ttlMs - elapsed) / 1000))
    /// </summary>
    internal static int CooldownRemainingSeconds(string? timestamp, int cooldownMs)
    {
        if (timestamp is null) return 0;
        if (!DateTimeOffset.TryParse(timestamp, out var refreshedAt)) return 0;

        var elapsed = (DateTimeOffset.UtcNow - refreshedAt).TotalMilliseconds;
        var remaining = cooldownMs - elapsed;
        return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining / 1000.0);
    }
}
