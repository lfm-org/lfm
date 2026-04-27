// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using Lfm.Api.Auth;
using Lfm.Api.Helpers;
using Lfm.Api.Middleware;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Validation;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Raiders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

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
///   5. Call <see cref="EnrichmentPlanner.Plan"/> to determine which tiers are stale.
///      If nothing is stale, reuse the cached record and skip Blizzard calls.
///   6. Otherwise call Blizzard for each stale tier only (profile, specializations,
///      media). Any failure returns 502. Results are merged with the existing record
///      so fresh tiers are not overwritten.
///   7. Upsert the character into <c>raider.Characters</c>, set
///      <c>SelectedCharacterId</c>, refresh the TTL, and save.
///   8. Return <see cref="AddCharacterResponse"/> with the mapped
///      <see cref="CharacterDto"/>.
/// </summary>
public class RaiderCharacterAddFunction(
    IRaidersRepository repo,
    IBlizzardProfileClient profileClient,
    ILogger<RaiderCharacterAddFunction> logger)
{
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
                return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }
        catch (JsonException)
        {
            // Never echo JsonException.Message — it can disclose offset/line/path
            // detail from the caller's payload that is not useful to the user.
            return Problem.BadRequest(req.HttpContext, "invalid-body", "Request body is invalid or missing.");
        }

        var validator = new AddCharacterRequestValidator();
        var validationResult = await validator.ValidateAsync(body, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToArray();
            return Problem.BadRequest(
                req.HttpContext,
                "validation-failed",
                "Request body failed validation.",
                new Dictionary<string, object?> { ["errors"] = errors });
        }

        // 2. Lowercase region/realm/name for id / ownership / storage.
        //    (Validator does not lowercase inputs — see plan, Task 3 carry-forward.)
        var region = body.Region!.ToLowerInvariant();
        var realm = body.Realm!.ToLowerInvariant();
        var lowerName = body.Name!.ToLowerInvariant();
        var characterId = $"{region}-{realm}-{lowerName}";

        // 3. Load the raider document.
        var raider = await repo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return Problem.NotFound(req.HttpContext, "raider-not-found", "Raider not found.");

        // 4. Ownership check against the cached account profile summary.
        if (!IsCharacterOwnedByAccount(characterId, region, raider.AccountProfileSummary))
        {
            logger.LogWarning(
                "Ownership check failed for {BattleNetId} on character {CharacterId}",
                principal.BattleNetId, characterId);
            return Problem.Forbidden(
                req.HttpContext,
                "character-not-in-bnet-account",
                "Character not found in your Battle.net account.");
        }

        // 5. Plan which tiers are stale; reuse the cached record if everything is fresh.
        var existing = raider.Characters?.FirstOrDefault(c => c.Id == characterId);
        var plan = EnrichmentPlanner.Plan(existing, DateTimeOffset.UtcNow);
        StoredSelectedCharacter stored;

        if (!plan.AnythingToFetch)
        {
            stored = existing!;
        }
        else
        {
            // 6. Fetch only stale tiers from Blizzard.  Access token must be present.
            var accessToken = principal.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                return Problem.Unauthorized(
                    req.HttpContext,
                    "missing-access-token",
                    "Session does not contain an access token. Please log out and log in again.");

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
            catch (HttpRequestException)
            {
                return Problem.UpstreamFailed(
                    req.HttpContext,
                    "blizzard-upstream-failed",
                    "Failed to fetch character from Blizzard.");
            }

            var now = DateTimeOffset.UtcNow.ToString("O");
            stored = Merge(existing, characterId, region, realm, body.Name!, profile, specs, media, now, plan);
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

    /// <summary>
    /// <c>/api/v1/raider/character</c> alias for <see cref="Run"/>. See
    /// <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("raider-character-add-v1")]
    [RequireAuth]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/raider/character")] HttpRequest req,
        FunctionContext ctx,
        CancellationToken ct)
        => Run(req, ctx, ct);

    // ---------------------------------------------------------------------------
    // Internal helpers — internal for unit-test access
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Merges Blizzard API responses with an existing stored character, preserving tiers
    /// that were not fetched (i.e. were still within their TTL window).
    /// Only updates <c>ProfileFetchedAt</c>, <c>SpecsFetchedAt</c>, and <c>MediaFetchedAt</c>
    /// for the tiers that were actually fetched; leaves legacy <c>FetchedAt</c> unchanged.
    /// </summary>
    internal static StoredSelectedCharacter Merge(
        StoredSelectedCharacter? existing,
        string id, string region, string realm, string nameDisplay,
        BlizzardCharacterProfileResponse? profile,
        BlizzardCharacterSpecializationsResponse? specs,
        BlizzardCharacterMediaSummary? media,
        string now,
        EnrichmentPlan plan)
    {
        var classId = profile?.CharacterClass?.Id ?? existing?.ClassId;
        var className = profile?.CharacterClass?.Name ?? existing?.ClassName;
        var level = profile?.Level ?? existing?.Level;
        var guildId = profile?.Guild?.Id ?? existing?.GuildId;
        var guildName = profile?.Guild?.Name ?? existing?.GuildName;
        var specs2 = specs is not null ? MapSpecializationsSummary(specs) : existing?.SpecializationsSummary;
        var media2 = media ?? existing?.MediaSummary;
        var portrait = media is not null ? PickPortraitUrl(media) : existing?.PortraitUrl;

        return new StoredSelectedCharacter(
            Id: id, Region: region, Realm: realm, Name: nameDisplay,
            PortraitUrl: portrait,
            SpecializationsSummary: specs2,
            MediaSummary: media2,
            ClassId: classId,
            ClassName: className,
            Level: level,
            GuildId: guildId,
            GuildName: guildName,
            FetchedAt: existing?.FetchedAt, // leave legacy alone
            ProfileFetchedAt: plan.FetchProfile ? now : existing?.ProfileFetchedAt,
            SpecsFetchedAt: plan.FetchSpecs ? now : existing?.SpecsFetchedAt,
            MediaFetchedAt: plan.FetchMedia ? now : existing?.MediaFetchedAt);
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
