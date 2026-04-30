// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.E2E.Seeds;

internal sealed class RaiderSeedBuilder
{
    private const string IdKey = "id";
    private const string BattleNetIdKey = "battleNetId";
    private const string SelectedCharacterIdKey = "selectedCharacterId";
    private const string LocaleKey = "locale";
    private const string LastSeenAtKey = "lastSeenAt";
    private const string TtlKey = "ttl";
    private const string AccountProfileRefreshedAtKey = "accountProfileRefreshedAt";
    private const string AccountProfileFetchedAtKey = "accountProfileFetchedAt";
    private const string CharactersKey = "characters";
    private const string AccountProfileSummaryKey = "accountProfileSummary";
    private const string WowAccountsKey = "wowAccounts";
    private const string PlayableClassKey = "playableClass";
    private const string SpecializationsSummaryKey = "specializationsSummary";
    private const string ActiveSpecializationKey = "activeSpecialization";
    private const string SpecializationsKey = "specializations";
    private const string SpecializationKey = "specialization";
    private const string GuildIdKey = "guildId";
    private const string GuildNameKey = "guildName";

    private const string Region = "eu";
    private const string RealmSlug = "test-realm";
    private const string RealmName = "Test Realm";
    private const int Level = 80;
    private const int GuildId = 12345;
    private const string GuildName = "Test Guild";
    private const string LastSeenAt = "2026-03-18T12:00:00.0000000Z";

    private readonly string battleNetId;
    private readonly int accountId;
    private readonly List<CharacterSeed> characters = [];

    public RaiderSeedBuilder(string battleNetId, int accountId)
    {
        this.battleNetId = battleNetId;
        this.accountId = accountId;
    }

    public RaiderSeedBuilder AddCharacter(
        string id,
        string name,
        int classId,
        string className,
        int specializationId,
        string specializationName)
    {
        characters.Add(new CharacterSeed(
            id,
            name,
            classId,
            className,
            specializationId,
            specializationName));

        return this;
    }

    public Dictionary<string, object?> Build()
    {
        if (characters.Count == 0)
        {
            throw new InvalidOperationException("At least one character is required.");
        }

        var now = DateTimeOffset.UtcNow.ToString("O");

        return new Dictionary<string, object?>
        {
            [IdKey] = battleNetId,
            [BattleNetIdKey] = battleNetId,
            [SelectedCharacterIdKey] = characters[0].Id,
            [LocaleKey] = null,
            [LastSeenAtKey] = LastSeenAt,
            [TtlKey] = -1,
            [AccountProfileRefreshedAtKey] = now,
            [AccountProfileFetchedAtKey] = now,
            [CharactersKey] = characters.Select(BuildRaiderCharacter).ToList(),
            [AccountProfileSummaryKey] = new Dictionary<string, object?>
            {
                [WowAccountsKey] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        [IdKey] = accountId,
                        [CharactersKey] = characters.Select(BuildAccountCharacter).ToList(),
                    },
                },
            },
        };
    }

    private static Dictionary<string, object?> BuildRaiderCharacter(CharacterSeed character)
    {
        return new Dictionary<string, object?>
        {
            [IdKey] = character.Id,
            ["region"] = Region,
            ["realm"] = RealmSlug,
            ["name"] = character.Name,
            ["portraitUrl"] = null,
            [SpecializationsSummaryKey] = new Dictionary<string, object?>
            {
                [ActiveSpecializationKey] = BuildSpecialization(character),
                [SpecializationsKey] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        [SpecializationKey] = BuildSpecialization(character),
                    },
                },
            },
            [GuildIdKey] = GuildId,
            [GuildNameKey] = GuildName,
        };
    }

    private static Dictionary<string, object?> BuildAccountCharacter(CharacterSeed character)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = character.Name,
            ["level"] = Level,
            ["realm"] = new Dictionary<string, object?>
            {
                ["slug"] = RealmSlug,
                ["name"] = RealmName,
            },
            [PlayableClassKey] = new Dictionary<string, object?>
            {
                [IdKey] = character.ClassId,
                ["name"] = character.ClassName,
            },
        };
    }

    private static Dictionary<string, object?> BuildSpecialization(CharacterSeed character)
    {
        return new Dictionary<string, object?>
        {
            [IdKey] = character.SpecializationId,
            ["name"] = character.SpecializationName,
        };
    }

    private sealed record CharacterSeed(
        string Id,
        string Name,
        int ClassId,
        string ClassName,
        int SpecializationId,
        string SpecializationName);
}
