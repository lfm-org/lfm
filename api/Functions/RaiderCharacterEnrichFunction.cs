// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Services.Blizzard.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Functions;

/// <summary>
/// POST /api/raider/characters/{id}/enrich — populates raider.Characters[id] with
/// fresh Blizzard data per the tiered TTL plan, without mutating SelectedCharacterId.
/// </summary>
public sealed class RaiderCharacterEnrichFunction(
    IRaidersRepository repo,
    IBlizzardProfileClient profileClient,
    IBlizzardRateLimiter rateLimiter,
    ILogger<RaiderCharacterEnrichFunction> logger)
{
    [Function("raider-character-enrich")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "raider/characters/{id}/enrich")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal();

        // Parse {id} as "{region}-{realm-slug}-{name}" (lowercased). Realm slugs may
        // contain dashes (e.g. "the-maelstrom"); character names cannot. First/last
        // dash positions uniquely identify the region and name boundaries.
        var firstDash = id.IndexOf('-');
        var lastDash = id.LastIndexOf('-');
        if (firstDash <= 0 || lastDash <= firstDash)
            return Problem.BadRequest(req.HttpContext, "invalid-character-id", "Invalid character id.");
        var region = id[..firstDash].ToLowerInvariant();
        var realm = id[(firstDash + 1)..lastDash].ToLowerInvariant();
        var lowerName = id[(lastDash + 1)..].ToLowerInvariant();

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        if (!RaiderCharacterAddFunction.IsCharacterOwnedByAccount(id, region, raider.AccountProfileSummary))
        {
            logger.LogWarning(
                "Ownership check failed for {BattleNetId} on character {CharacterId}",
                principal.BattleNetId, id);
            return Problem.Forbidden(
                req.HttpContext,
                "character-not-in-bnet-account",
                "Character not found in your Battle.net account.");
        }

        var existing = raider.Characters?.FirstOrDefault(c => c.Id == id);
        var plan = EnrichmentPlanner.Plan(existing, DateTimeOffset.UtcNow);

        StoredSelectedCharacter stored;
        if (!plan.AnythingToFetch && existing is not null)
        {
            stored = existing;
        }
        else
        {
            var accessToken = principal.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return Problem.Unauthorized(
                    req.HttpContext,
                    "missing-access-token",
                    "Session does not contain an access token. Please log out and log in again.");

            CharacterProfileResponse? profile = null;
            CharacterSpecializationsResponse? specs = null;
            CharacterMediaSummaryResponse? media = null;
            try
            {
                if (plan.FetchProfile)
                    profile = await profileClient.GetCharacterProfileAsync(realm, lowerName, accessToken, ct);
                if (plan.FetchSpecs)
                    specs = await profileClient.GetCharacterSpecializationsAsync(realm, lowerName, accessToken, ct);
                if (plan.FetchMedia)
                    media = await profileClient.GetCharacterMediaAsync(realm, lowerName, accessToken, ct);
            }
            catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
            {
                // Surface the shared rate-limiter's pause budget as RFC 9110
                // Retry-After so clients schedule the retry instead of
                // guessing. Ceiling to the next whole second — a sub-second
                // Retry-After is out of spec and would round down to 0.
                var pause = rateLimiter.RemainingPause;
                var retryAfterSeconds = pause is TimeSpan p
                    ? (int)Math.Ceiling(p.TotalSeconds)
                    : (int?)null;
                return Problem.TooManyRequests(
                    req.HttpContext,
                    "upstream-rate-limited",
                    "Upstream rate-limited.",
                    retryAfterSeconds);
            }
            catch (HttpRequestException ex) when ((int?)ex.StatusCode == 404)
            {
                return Problem.NotFound(
                    req.HttpContext,
                    "character-not-found-on-blizzard",
                    "Character not found on Blizzard.");
            }
            catch (HttpRequestException)
            {
                return Problem.UpstreamFailed(
                    req.HttpContext,
                    "blizzard-upstream-failed",
                    "Failed to fetch character from Blizzard.");
            }

            var nowIso = DateTimeOffset.UtcNow.ToString("O");
            var originalName = existing?.Name ?? lowerName;
            stored = RaiderCharacterAddFunction.Merge(
                existing, id, region, realm, originalName, profile, specs, media, nowIso, plan);
        }

        var updatedCharacters = (raider.Characters ?? []).ToList();
        var idx = updatedCharacters.FindIndex(c => c.Id == id);
        if (idx >= 0) updatedCharacters[idx] = stored; else updatedCharacters.Add(stored);

        // Preserve SelectedCharacterId — do NOT mutate it.
        var updated = raider with { Characters = updatedCharacters };
        await repo.UpsertAsync(updated, ct);

        return new OkObjectResult(RaiderCharacterAddFunction.MapToCharacterDto(stored));
    }

    /// <summary>
    /// <c>/api/v1/raider/characters/{id}/enrich</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("raider-character-enrich-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/raider/characters/{id}/enrich")] HttpRequest req,
        string id,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, id, ctx, ct);
}
