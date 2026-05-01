// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Characters;

namespace Lfm.Api.Services;

public sealed record GuildRosterMatch(int Rank, string CharacterKey);

public static class GuildRosterMatcher
{
    public static GuildRosterMatch? Match(StoredGuildRoster? roster, StoredSelectedCharacter character) =>
        Match(roster, character.Realm, character.Name);

    public static GuildRosterMatch? Match(StoredGuildRoster? roster, CharacterDto character) =>
        Match(roster, character.Realm, character.Name);

    public static GuildRosterMatch? Match(StoredGuildRoster? roster, string realmSlug, string characterName)
    {
        if (roster?.Members is null)
            return null;

        var wanted = CharacterKey(realmSlug, characterName);
        foreach (var member in roster.Members)
        {
            var key = CharacterKey(member.Character.Realm.Slug, member.Character.Name);
            if (StringComparer.OrdinalIgnoreCase.Equals(key, wanted))
                return new GuildRosterMatch(member.Rank, key);
        }

        return null;
    }

    public static int? BestRank(StoredGuildRoster? roster, IEnumerable<StoredSelectedCharacter>? characters)
    {
        if (roster?.Members is null || characters is null)
            return null;

        int? bestRank = null;
        foreach (var character in characters)
        {
            var match = Match(roster, character);
            if (match is not null && (bestRank is null || match.Rank < bestRank.Value))
                bestRank = match.Rank;
        }

        return bestRank;
    }

    public static bool IsFresh(string? fetchedAt)
    {
        if (fetchedAt is null)
            return false;
        if (!DateTimeOffset.TryParse(fetchedAt, out var parsed))
            return false;

        return DateTimeOffset.UtcNow - parsed < TimeSpan.FromHours(1);
    }

    private static string CharacterKey(string realmSlug, string characterName) =>
        $"{realmSlug.ToLowerInvariant()}:{characterName.ToLowerInvariant()}";
}
