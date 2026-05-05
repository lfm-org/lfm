// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Characters;
using Lfm.Contracts.WoW;

namespace Lfm.Api.Mappers;

internal static class AccountCharacterMapper
{
    /// <summary>
    /// Maps a cached Blizzard account profile summary to account-owned character
    /// views. Mirrors <c>toAccountCharacterViews</c> in
    /// <c>functions/src/lib/blizzard-adapters.ts</c>.
    /// </summary>
    internal static List<CharacterDto> MapToCharacterDtos(
        StoredBlizzardAccountProfile summary,
        string region,
        IReadOnlyList<StoredSelectedCharacter>? storedCharacters,
        IReadOnlyDictionary<string, string>? portraitCache)
    {
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

                // Prefer stored demographics populated during character refresh,
                // falling back to the Blizzard account-character fields.
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

                var specializations = stored?.SpecializationsSummary?.Specializations?
                    .Select(s => new CharacterSpecializationDto(
                        Id: s.Specialization.Id,
                        Name: s.Specialization.Name ?? string.Empty))
                    .ToList();

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
                    SpecName: specName,
                    Specializations: specializations));
            }

        return result;
    }

    /// <summary>
    /// Returns true when the URL is a Blizzard render URL. Mirrors
    /// <c>isBlizzardRenderUrl</c> in
    /// <c>functions/src/lib/character-portrait.ts</c>.
    /// </summary>
    private static bool IsBlizzardRenderUrl(string url)
        => url.StartsWith("https://render.worldofwarcraft.com/", StringComparison.OrdinalIgnoreCase);
}
