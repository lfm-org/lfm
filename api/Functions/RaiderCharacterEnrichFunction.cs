// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
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
            return new BadRequestObjectResult(new { error = "Invalid character id" });
        var region = id[..firstDash].ToLowerInvariant();
        var realm = id[(firstDash + 1)..lastDash].ToLowerInvariant();
        var lowerName = id[(lastDash + 1)..].ToLowerInvariant();

        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new NotFoundObjectResult(new { error = "Raider not found" });

        if (!RaiderCharacterAddFunction.IsCharacterOwnedByAccount(id, region, raider.AccountProfileSummary))
        {
            logger.LogWarning(
                "Ownership check failed for {BattleNetId} on character {CharacterId}",
                principal.BattleNetId, id);
            return new ObjectResult(new { error = "Character not found in your Battle.net account" })
            { StatusCode = 403 };
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
                return new ObjectResult(new { error = "Session does not contain an access token. Please log out and log in again." })
                { StatusCode = 401 };

            BlizzardCharacterProfileResponse? profile = null;
            BlizzardCharacterSpecializationsResponse? specs = null;
            BlizzardCharacterMediaSummary? media = null;
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
                return new ObjectResult(new { error = "Upstream rate-limited" }) { StatusCode = 429 };
            }
            catch (HttpRequestException)
            {
                return new ObjectResult(new { error = "Failed to fetch character from Blizzard" }) { StatusCode = 502 };
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
}
