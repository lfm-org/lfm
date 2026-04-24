// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Contracts.Characters;
using Lfm.Contracts.WoW;
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
        var characters = MapToCharacterDtos(raider.AccountProfileSummary!, region, raider.Characters, raider.PortraitCache);

        req.HttpContext.Response.Headers["Cache-Control"] = "private, max-age=300";
        return new OkObjectResult(characters);
    }

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

    /// <summary>
    /// Maps a cached Blizzard account profile summary to a list of <see cref="CharacterDto"/>s.
    /// Mirrors <c>toAccountCharacterViews</c> in
    /// <c>functions/src/lib/blizzard-adapters.ts</c>.
    /// </summary>
    internal static List<CharacterDto> MapToCharacterDtos(
        BlizzardAccountProfileSummary summary,
        string region,
        IReadOnlyList<StoredSelectedCharacter>? storedCharacters,
        IReadOnlyDictionary<string, string>? portraitCache)
    {
        // Build a lookup of stored characters keyed by "name:realm".
        var storedByKey = new Dictionary<string, StoredSelectedCharacter>(StringComparer.OrdinalIgnoreCase);
        if (storedCharacters is not null)
        {
            foreach (var sc in storedCharacters)
                storedByKey[$"{sc.Name.ToLowerInvariant()}:{sc.Realm.ToLowerInvariant()}"] = sc;
        }

        var result = new List<CharacterDto>();
        foreach (var account in summary.WowAccounts ?? [])
            foreach (var character in account.Characters ?? [])
            {
                var realmSlug = character.Realm.Slug.ToLowerInvariant();
                var realmName = character.Realm.Name ?? character.Realm.Slug;
                storedByKey.TryGetValue($"{character.Name.ToLowerInvariant()}:{realmSlug}", out var stored);

                // Prefer stored demographics (populated during character refresh),
                // fall back to the Blizzard account-character field.
                var classId = stored?.ClassId ?? character.PlayableClass?.Id;
                var className = stored?.ClassName
                    ?? (classId is int cid ? WowClasses.GetName(cid) : null);

                var cachedId = $"{region}-{character.Realm.Slug.ToLowerInvariant()}-{character.Name.ToLowerInvariant()}";
                string? portraitUrl = null;
                if (stored?.PortraitUrl is not null)
                {
                    portraitUrl = stored.PortraitUrl;
                }
                else if (portraitCache is not null && portraitCache.TryGetValue(cachedId, out var cached)
                         && IsBlizzardRenderUrl(cached))
                {
                    portraitUrl = cached;
                }

                var activeSpecId = stored?.SpecializationsSummary?.ActiveSpecialization?.Id;
                string? specName = null;
                if (activeSpecId is not null && stored?.SpecializationsSummary?.Specializations is not null)
                {
                    var spec = stored.SpecializationsSummary.Specializations
                        .FirstOrDefault(s => s.Specialization.Id == activeSpecId);
                    specName = spec?.Specialization.Name;
                }

                result.Add(new CharacterDto(
                    Name: character.Name,
                    Realm: character.Realm.Slug,
                    RealmName: realmName,
                    Level: character.Level,
                    Region: region,
                    ClassId: classId,
                    ClassName: className,
                    PortraitUrl: portraitUrl,
                    ActiveSpecId: activeSpecId,
                    SpecName: specName));
            }

        return result;
    }

    /// <summary>
    /// Returns true when the URL is a Blizzard render URL.
    /// Mirrors <c>isBlizzardRenderUrl</c> in
    /// <c>functions/src/lib/character-portrait.ts</c>.
    /// </summary>
    private static bool IsBlizzardRenderUrl(string url)
        => url.StartsWith("https://render.worldofwarcraft.com/", StringComparison.OrdinalIgnoreCase);
}
