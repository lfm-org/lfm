// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Contracts.Characters;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="ICharacterPortraitService"/> by resolving portrait URLs from
/// the raider document cache or, on a cache miss, by fetching from the Blizzard
/// character-media API.
///
/// Mirrors the handler logic in
/// <c>functions/src/functions/battlenet-character-portraits.ts</c>.
/// </summary>
public sealed class CharacterPortraitService(
    IRaidersRepository repo,
    HttpClient httpClient,
    IOptions<BlizzardOptions> blizzardOptions) : ICharacterPortraitService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _region = blizzardOptions.Value.Region.ToLowerInvariant();

    /// <inheritdoc />
    public async Task<PortraitResponse> ResolveAsync(
        RaiderDocument raider,
        IReadOnlyList<CharacterPortraitRequest> requests,
        string accessToken,
        CancellationToken ct)
    {
        // Working copies — mutated as portraits are resolved.
        var portraitCache = raider.PortraitCache is not null
            ? new Dictionary<string, string>(raider.PortraitCache)
            : new Dictionary<string, string>();
        var characters = raider.Characters is not null
            ? [.. raider.Characters]
            : new List<StoredSelectedCharacter>();
        var result = new Dictionary<string, string>();
        var toFetch = new List<(string Region, string Realm, string Name, string Id)>();
        var cacheUpdated = false;

        foreach (var req in requests)
        {
            // Validate and normalise inputs (mirrors TS validateRegion/validateRealmSlug/validateCharacterName).
            if (!TryNormalise(req, out var normRegion, out var normRealm, out var normName))
                continue;

            var characterId = $"{normRegion}-{normRealm}-{normName}";

            // Step 1: check stored character (selected character with cached media).
            var storedIndex = characters.FindIndex(c => c.Id == characterId);
            var stored = storedIndex >= 0 ? characters[storedIndex] : null;

            var storedPortraitUrl = ResolveFromStored(stored);
            if (storedPortraitUrl is not null)
            {
                result[characterId] = storedPortraitUrl;
                if (stored is not null && stored.PortraitUrl != storedPortraitUrl)
                {
                    characters[storedIndex] = stored with { PortraitUrl = storedPortraitUrl };
                    cacheUpdated = true;
                }
                if (!portraitCache.TryGetValue(characterId, out var existingCached) || existingCached != storedPortraitUrl)
                {
                    portraitCache[characterId] = storedPortraitUrl;
                    cacheUpdated = true;
                }
                continue;
            }

            // Step 2: check portraitCache.
            if (portraitCache.TryGetValue(characterId, out var cachedUrl) && IsBlizzardRenderUrl(cachedUrl))
            {
                result[characterId] = cachedUrl;
                if (stored is not null && stored.PortraitUrl != cachedUrl)
                {
                    characters[storedIndex] = stored with { PortraitUrl = cachedUrl };
                    cacheUpdated = true;
                }
                continue;
            }

            // Step 3: needs a Blizzard API call.
            toFetch.Add((normRegion, normRealm, normName, characterId));
        }

        // Step 4: fetch missing portraits from Blizzard in parallel.
        if (toFetch.Count > 0)
        {
            var fetchTasks = toFetch
                .Select(c => FetchPortraitAsync(c.Region, c.Realm, c.Name, c.Id, accessToken, ct))
                .ToArray();

            var outcomes = new List<FetchResult?>();
            foreach (var task in fetchTasks)
            {
                try { outcomes.Add(await task); }
                catch (HttpRequestException ex) when ((int?)ex.StatusCode == 429)
                {
                    throw; // bubble to caller; let the endpoint return 429 with Retry-After
                }
                catch { outcomes.Add(null); }
            }

            foreach (var outcome in outcomes)
            {
                if (outcome is not null)
                {
                    result[outcome.Id] = outcome.Url;
                    portraitCache[outcome.Id] = outcome.Url;
                    cacheUpdated = true;
                }
            }
        }

        // Persist raider if cache was updated.
        if (cacheUpdated)
        {
            var updated = raider with
            {
                Characters = characters,
                PortraitCache = portraitCache,
                Ttl = 180 * 86400,
            };
            await repo.UpsertAsync(updated, ct);
        }

        return new PortraitResponse(result);
    }

    // ---------------------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Validates and normalises the request fields.
    /// Mirrors <c>validateRegion</c>, <c>validateRealmSlug</c>, <c>validateCharacterName</c>
    /// from <c>functions/src/lib/blizzard-validation.ts</c>.
    /// </summary>
    private static bool TryNormalise(
        CharacterPortraitRequest req,
        out string region,
        out string realm,
        out string name)
    {
        region = realm = name = string.Empty;

        var normRegion = req.Region?.ToLowerInvariant() ?? string.Empty;
        if (!IsValidRegion(normRegion)) return false;

        var normRealm = req.Realm?.ToLowerInvariant() ?? string.Empty;
        if (!IsValidRealmSlug(normRealm)) return false;

        var normName = req.Name?.ToLowerInvariant() ?? string.Empty;
        if (!IsValidCharacterName(req.Name ?? string.Empty)) return false;

        region = normRegion;
        realm = normRealm;
        name = normName;
        return true;
    }

    private static readonly HashSet<string> ValidRegions = ["eu", "us", "kr", "tw", "cn"];
    private static bool IsValidRegion(string r) => ValidRegions.Contains(r);
    private static bool IsValidRealmSlug(string s)
        => s.Length > 0 && s.Length <= 64 && s.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    private static bool IsValidCharacterName(string s)
    {
        if (s.Length < 2 || s.Length > 12) return false;
        foreach (var c in s)
            if (!char.IsLetter(c)) return false;
        return true;
    }

    /// <summary>
    /// Resolves a portrait URL from the stored character's <c>portraitUrl</c> or
    /// <c>mediaSummary.assets[key="avatar"]</c>.
    /// Returns null when no valid CDN URL is available.
    ///
    /// Mirrors the TS logic:
    ///   stored.portraitUrl (if isBlizzardRenderUrl) OR findAvatarUrl(stored.mediaSummary)
    /// </summary>
    private static string? ResolveFromStored(StoredSelectedCharacter? stored)
    {
        if (stored is null) return null;

        if (stored.PortraitUrl is not null && IsBlizzardRenderUrl(stored.PortraitUrl))
            return stored.PortraitUrl;

        if (stored.MediaSummary?.Assets is not null)
        {
            foreach (var asset in stored.MediaSummary.Assets)
                if (asset.Key == "avatar" && !string.IsNullOrEmpty(asset.Value))
                    return asset.Value;
        }

        return null;
    }

    /// <summary>
    /// Returns true when the URL is a Blizzard render CDN URL.
    /// Mirrors <c>isBlizzardRenderUrl</c> from
    /// <c>functions/src/lib/character-portrait.ts</c>.
    /// </summary>
    private static bool IsBlizzardRenderUrl(string url)
        => url.StartsWith("https://render.worldofwarcraft.com/", StringComparison.OrdinalIgnoreCase);

    private sealed record FetchResult(string Id, string Url);

    private async Task<FetchResult?> FetchPortraitAsync(
        string charRegion, string realm, string name, string characterId,
        string accessToken, CancellationToken ct)
    {
        // The typed HttpClient's BaseAddress is set per region in Program.cs:
        //   https://{region}.api.blizzard.com/
        // However the character-media endpoint requires the *character's* region, not the
        // configured one.  We build the full URL to support cross-region characters.
        var encodedRealm = Uri.EscapeDataString(realm);
        var encodedName = Uri.EscapeDataString(name);
        var namespace_ = $"profile-{charRegion}";
        var url = $"https://{charRegion}.api.blizzard.com/profile/wow/character/{encodedRealm}/{encodedName}/character-media?namespace={Uri.EscapeDataString(namespace_)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, ct);
        if ((int)response.StatusCode == 429)
            throw new HttpRequestException("Upstream 429", inner: null, statusCode: response.StatusCode);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        var media = JsonSerializer.Deserialize<BlizzardCharacterMediaResponse>(json, _jsonOptions);
        var avatarUrl = FindAvatarUrl(media);
        if (string.IsNullOrEmpty(avatarUrl)) return null;

        return new FetchResult(characterId, avatarUrl);
    }

    private static string? FindAvatarUrl(BlizzardCharacterMediaResponse? media)
    {
        if (media?.Assets is null) return null;
        foreach (var asset in media.Assets)
            if (asset.Key == "avatar" && !string.IsNullOrEmpty(asset.Value))
                return asset.Value;
        return null;
    }

    // ---------------------------------------------------------------------------
    // Local Blizzard response model (only used within this service)
    // ---------------------------------------------------------------------------

    private sealed record BlizzardCharacterMediaAssetResponse(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("value")] string Value);

    private sealed record BlizzardCharacterMediaResponse(
        [property: JsonPropertyName("assets")] IReadOnlyList<BlizzardCharacterMediaAssetResponse>? Assets = null);
}
