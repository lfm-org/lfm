// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Auth;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Raiders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// POST /api/raider/character — fetches a character from the Blizzard Profile API,
/// stores it on the raider document, and marks it as the selected character.
///
/// Logic (mirrors <c>functions/src/functions/raider-character.ts</c> <c>handler</c>):
///   1. Parse and validate the request body.
///   2. Lowercase region/realm/name for internal use (id + ownership + storage).
///   3. Load the raider document — 404 if missing.
///   4. Verify the character belongs to the raider's cached Battle.net account
///      profile — 403 if not. A null profile (new raider) is allowed through.
///   5. If the character is already stored and its <c>FetchedAt</c> is within the
///      15-minute cache window, reuse the cached record and skip Blizzard calls.
///   6. Otherwise call Blizzard for profile, specializations and (best-effort) media.
///      Any failure of the required calls returns 502.
///   7. Upsert the character into <c>raider.Characters</c>, set
///      <c>SelectedCharacterId</c>, refresh the TTL, and save.
///   8. Return <see cref="AddCharacterResponse"/> with the mapped
///      <see cref="CharacterDto"/>.
/// </summary>
public class RaiderCharacterAddFunction(
    IRaidersRepository repo,
    IBlizzardProfileClient profileClient)
{
    // 15 minutes in milliseconds — mirrors CHARACTER_PROFILE_TTL_MS in cache.ts.
    internal const int CharacterProfileTtlMs = 15 * 60 * 1000;

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("raider-character-add")]
    [RequireAuth]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "raider/character")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
    {
        var principal = ctx.GetPrincipal();

        // 1. Parse and validate the request body.
        AddCharacterRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AddCharacterRequest>(
                req.Body, JsonOptions, cancellationToken: ct);
            if (body is null)
                return new BadRequestObjectResult(new { error = "Invalid request body" });
        }
        catch (JsonException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }

        var validator = new AddCharacterRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
            return new BadRequestObjectResult(
                new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        // 2. Lowercase region/realm/name for id / ownership / storage.
        //    (Validator does not lowercase inputs — see plan, Task 3 carry-forward.)
        var region = body.Region!.ToLowerInvariant();
        var realm = body.Realm!.ToLowerInvariant();
        var lowerName = body.Name!.ToLowerInvariant();
        var characterId = $"{region}-{realm}-{lowerName}";

        // 3. Load the raider document.
        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new NotFoundObjectResult(new { error = "Raider not found" });

        // 4. Ownership check against the cached account profile summary.
        if (!IsCharacterOwnedByAccount(characterId, region, raider.AccountProfileSummary))
            return new ObjectResult(new { error = "Character not found in your Battle.net account" })
            { StatusCode = 403 };

        // 5. Cache lookup — reuse when fresh.
        var existing = raider.Characters?.FirstOrDefault(c => c.Id == characterId);
        StoredSelectedCharacter stored;

        if (CanReuseCachedCharacter(existing))
        {
            stored = existing!;
        }
        else
        {
            // 6. Cache miss — fetch from Blizzard.  Access token must be present.
            var accessToken = principal.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return new ObjectResult(new { error = "Session does not contain an access token. Please log out and log in again." })
                { StatusCode = 401 };

            BlizzardCharacterProfileResponse profile;
            BlizzardCharacterSpecializationsResponse specs;
            BlizzardCharacterMediaSummary? media;
            try
            {
                profile = await profileClient.GetCharacterProfileAsync(realm, lowerName, accessToken, ct);
                specs = await profileClient.GetCharacterSpecializationsAsync(realm, lowerName, accessToken, ct);
                // Media is best-effort: client returns null on any failure.
                media = await profileClient.GetCharacterMediaAsync(realm, lowerName, accessToken, ct);
            }
            catch (HttpRequestException)
            {
                return new ObjectResult(new { error = "Failed to fetch character from Blizzard" })
                { StatusCode = 502 };
            }

            stored = new StoredSelectedCharacter(
                Id: characterId,
                Region: region,
                Realm: realm,
                Name: body.Name!, // keep original casing for display
                PortraitUrl: PickPortraitUrl(media),
                SpecializationsSummary: MapSpecializationsSummary(specs),
                MediaSummary: media,
                ClassId: profile.CharacterClass?.Id,
                ClassName: profile.CharacterClass?.Name,
                Level: profile.Level,
                GuildId: profile.Guild?.Id,
                GuildName: profile.Guild?.Name,
                FetchedAt: DateTimeOffset.UtcNow.ToString("O"));
        }

        // 7. Upsert the character into raider.Characters and persist.
        var updatedCharacters = (raider.Characters ?? []).ToList();
        var existingIdx = updatedCharacters.FindIndex(c => c.Id == characterId);
        if (existingIdx >= 0)
            updatedCharacters[existingIdx] = stored;
        else
            updatedCharacters.Add(stored);

        var updatedPortraitCache = stored.PortraitUrl is not null
            ? new Dictionary<string, string>(raider.PortraitCache ?? new Dictionary<string, string>())
            { [characterId] = stored.PortraitUrl }
            : raider.PortraitCache;

        var updatedRaider = raider with
        {
            Characters = updatedCharacters,
            SelectedCharacterId = characterId,
            PortraitCache = updatedPortraitCache,
            Ttl = 180 * 86400,
        };
        await repo.UpsertAsync(updatedRaider, ct);

        // 8. Map to CharacterDto and return.
        var dto = MapToCharacterDto(stored);
        return new OkObjectResult(new AddCharacterResponse(
            SelectedCharacterId: characterId,
            Character: dto));
    }

    // ---------------------------------------------------------------------------
    // Internal helpers — internal for unit-test access
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Returns true when an existing stored character can be reused without calling Blizzard.
    /// Mirrors <c>canReuseCachedCharacter</c> in <c>functions/src/functions/raider-character.ts</c>,
    /// except: when a cached character exists but lacks specializations, we trigger a full
    /// refetch rather than the TS optimization of fetching specs-only. The TS optimization is
    /// safe to add if needed — for now we prefer simplicity over saving one API call per user.
    /// </summary>
    internal static bool CanReuseCachedCharacter(StoredSelectedCharacter? existing)
    {
        if (existing?.SpecializationsSummary is null) return false;
        if (existing.FetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(existing.FetchedAt, out var fetchedAt)) return false;

        var elapsed = (DateTimeOffset.UtcNow - fetchedAt).TotalMilliseconds;
        return elapsed < CharacterProfileTtlMs;
    }

    /// <summary>
    /// Returns true when the given character id matches an entry in the cached
    /// Blizzard account profile summary, using an exact string compare against the
    /// full <c>{region}-{realm.slug}-{name}</c> id (all lowercased).
    /// A null profile is treated as "allow" — new raiders haven't yet synced their
    /// account profile, mirroring the TS handler's behaviour.
    /// </summary>
    internal static bool IsCharacterOwnedByAccount(
        string characterId,
        string region,
        BlizzardAccountProfileSummary? accountProfileSummary)
    {
        if (accountProfileSummary is null) return true;

        foreach (var account in accountProfileSummary.WowAccounts ?? [])
        {
            foreach (var ch in account.Characters ?? [])
            {
                var ownedId = $"{region}-{ch.Realm.Slug.ToLowerInvariant()}-{ch.Name.ToLowerInvariant()}";
                if (string.Equals(characterId, ownedId, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selects the avatar URL from a Blizzard media summary, falling back to the
    /// first asset if no key named "avatar" is present.  Returns null when the
    /// media summary is null or has no assets.
    /// </summary>
    internal static string? PickPortraitUrl(BlizzardCharacterMediaSummary? media)
    {
        if (media?.Assets is null || media.Assets.Count == 0) return null;
        var avatar = media.Assets.FirstOrDefault(a =>
            string.Equals(a.Key, "avatar", StringComparison.OrdinalIgnoreCase));
        return avatar?.Value ?? media.Assets[0].Value;
    }

    /// <summary>
    /// Maps a Blizzard specializations response into the stored camelCase-keyed
    /// shape the rest of the application uses.
    /// </summary>
    internal static StoredSpecializationsSummary MapSpecializationsSummary(
        BlizzardCharacterSpecializationsResponse specs)
    {
        var active = specs.ActiveSpecialization is null
            ? null
            : new StoredCharacterSpecialization(specs.ActiveSpecialization.Id, specs.ActiveSpecialization.Name);

        var list = specs.Specializations?
            .Select(s => new StoredSpecializationsEntry(
                new StoredCharacterSpecialization(s.Specialization.Id, s.Specialization.Name)))
            .ToList();

        return new StoredSpecializationsSummary(
            ActiveSpecialization: active,
            Specializations: list);
    }

    /// <summary>
    /// Maps a <see cref="StoredSelectedCharacter"/> to the outbound
    /// <see cref="CharacterDto"/>.  Mirrors the field selection used by
    /// <see cref="BattleNetCharactersFunction.MapToCharacterDtos"/>.
    /// </summary>
    internal static CharacterDto MapToCharacterDto(StoredSelectedCharacter stored)
    {
        var activeSpecId = stored.SpecializationsSummary?.ActiveSpecialization?.Id;
        string? specName = null;
        if (activeSpecId is not null && stored.SpecializationsSummary?.Specializations is not null)
        {
            var spec = stored.SpecializationsSummary.Specializations
                .FirstOrDefault(s => s.Specialization.Id == activeSpecId);
            specName = spec?.Specialization.Name;
        }

        return new CharacterDto(
            Name: stored.Name,
            Realm: stored.Realm,
            RealmName: stored.Realm,
            Level: stored.Level ?? 0,
            Region: stored.Region,
            ClassId: stored.ClassId,
            ClassName: stored.ClassName,
            PortraitUrl: stored.PortraitUrl,
            ActiveSpecId: activeSpecId,
            SpecName: specName);
    }
}
