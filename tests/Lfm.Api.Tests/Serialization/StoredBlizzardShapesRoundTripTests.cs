// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Lfm.Api.Tests.Serialization;

/// <summary>
/// Pins the Cosmos JSON keys for the renamed Stored* Blizzard shapes after the
/// SD-S-5 wire/persistence split. The Cosmos client uses Newtonsoft with a
/// camelCase property naming policy (see api/Program.cs CosmosSerializationOptions);
/// these tests exercise the same Newtonsoft path so a future rename of any
/// .NET property would surface as a key drift here, before reaching production.
///
/// Pre-rename, the dual-roled wire/storage records carried explicit STJ
/// <c>[JsonPropertyName("snake_case")]</c> attributes. Post-rename the storage
/// records rely on the Newtonsoft camelCase contract resolver alone, so a
/// regression here would silently change the on-disk Cosmos document layout.
/// </summary>
public class StoredBlizzardShapesRoundTripTests
{
    private static readonly JsonSerializerSettings CosmosLikeSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    [Fact]
    public void StoredBlizzardAccountProfile_RoundTrips_WithCamelCaseKeys()
    {
        var src = new StoredBlizzardAccountProfile(
            WowAccounts: new[]
            {
                new StoredBlizzardWowAccount(
                    Id: 42,
                    Characters: new[]
                    {
                        new StoredBlizzardAccountCharacter(
                            Name: "Alice",
                            Level: 80,
                            Realm: new StoredBlizzardRealmRef(Slug: "ravencrest", Name: "Ravencrest"),
                            PlayableClass: new StoredBlizzardNamedRef(Id: 1, Name: "Warrior")),
                    }),
            });

        var json = JsonConvert.SerializeObject(src, CosmosLikeSettings);
        var token = JObject.Parse(json);

        // Pin top-level + nested camelCase keys so a property rename in
        // StoredBlizzard* would surface here, not in production Cosmos drift.
        Assert.NotNull(token["wowAccounts"]);
        Assert.Equal(42, (int)token["wowAccounts"]![0]!["id"]!);
        Assert.NotNull(token["wowAccounts"]![0]!["characters"]);
        Assert.Equal("Alice", (string?)token["wowAccounts"]![0]!["characters"]![0]!["name"]);
        Assert.Equal(80, (int)token["wowAccounts"]![0]!["characters"]![0]!["level"]!);
        Assert.Equal("ravencrest", (string?)token["wowAccounts"]![0]!["characters"]![0]!["realm"]!["slug"]);
        Assert.Equal("Ravencrest", (string?)token["wowAccounts"]![0]!["characters"]![0]!["realm"]!["name"]);
        Assert.Equal(1, (int)token["wowAccounts"]![0]!["characters"]![0]!["playableClass"]!["id"]!);
        Assert.Equal("Warrior", (string?)token["wowAccounts"]![0]!["characters"]![0]!["playableClass"]!["name"]);

        var roundTripped = JsonConvert.DeserializeObject<StoredBlizzardAccountProfile>(json, CosmosLikeSettings);
        Assert.NotNull(roundTripped);
        Assert.Single(roundTripped!.WowAccounts!);
        Assert.Equal(42, roundTripped.WowAccounts![0].Id);
        Assert.Single(roundTripped.WowAccounts![0].Characters!);
        var character = roundTripped.WowAccounts![0].Characters![0];
        Assert.Equal("Alice", character.Name);
        Assert.Equal(80, character.Level);
        Assert.Equal("ravencrest", character.Realm.Slug);
        Assert.Equal("Ravencrest", character.Realm.Name);
        Assert.Equal(1, character.PlayableClass!.Id);
        Assert.Equal("Warrior", character.PlayableClass!.Name);
    }

    [Fact]
    public void StoredGuildRoster_RoundTrips_WithCamelCaseKeys()
    {
        var src = new StoredGuildRoster(
            Members: new[]
            {
                new StoredGuildRosterMember(
                    Character: new StoredGuildRosterMemberCharacter(
                        Name: "Bob",
                        Realm: new StoredGuildRosterRealm(Slug: "stormrage"),
                        Id: 7),
                    Rank: 3),
            });

        var json = JsonConvert.SerializeObject(src, CosmosLikeSettings);
        var token = JObject.Parse(json);

        // Pin top-level + nested camelCase keys for the guild roster shape.
        Assert.NotNull(token["members"]);
        Assert.Equal(3, (int)token["members"]![0]!["rank"]!);
        Assert.NotNull(token["members"]![0]!["character"]);
        Assert.Equal("Bob", (string?)token["members"]![0]!["character"]!["name"]);
        Assert.Equal(7, (int)token["members"]![0]!["character"]!["id"]!);
        Assert.Equal("stormrage", (string?)token["members"]![0]!["character"]!["realm"]!["slug"]);

        var roundTripped = JsonConvert.DeserializeObject<StoredGuildRoster>(json, CosmosLikeSettings);
        Assert.NotNull(roundTripped);
        Assert.Single(roundTripped!.Members!);
        var member = roundTripped.Members![0];
        Assert.Equal(3, member.Rank);
        Assert.Equal("Bob", member.Character.Name);
        Assert.Equal(7, member.Character.Id);
        Assert.Equal("stormrage", member.Character.Realm.Slug);
    }

    [Fact]
    public void StoredGuildProfile_RoundTrips_WithCamelCaseKeys()
    {
        // Note: StoredGuildProfile.Name carries [JsonConverter(LocalizedStringConverter)].
        // The plain-string write-path emits a JSON string, so the round-trip below
        // exercises the simple-string case (the legacy localized-object case is
        // covered separately in LocalizedStringConverterTests).
        var src = new StoredGuildProfile(
            Name: "Knights of the Round",
            Realm: new StoredGuildProfileRealm(Slug: "ravencrest", Name: "Ravencrest"),
            Faction: new StoredGuildProfileFaction(Name: "Horde"),
            MemberCount: 250,
            AchievementPoints: 12345);

        var json = JsonConvert.SerializeObject(src, CosmosLikeSettings);
        var token = JObject.Parse(json);

        // Pin top-level camelCase keys on the guild profile shape.
        Assert.Equal("Knights of the Round", (string?)token["name"]);
        Assert.Equal("Horde", (string?)token["faction"]!["name"]);
        Assert.Equal("ravencrest", (string?)token["realm"]!["slug"]);
        Assert.Equal("Ravencrest", (string?)token["realm"]!["name"]);
        Assert.Equal(250, (int)token["memberCount"]!);
        Assert.Equal(12345, (int)token["achievementPoints"]!);

        var roundTripped = JsonConvert.DeserializeObject<StoredGuildProfile>(json, CosmosLikeSettings);
        Assert.NotNull(roundTripped);
        Assert.Equal("Knights of the Round", roundTripped!.Name);
        Assert.Equal("ravencrest", roundTripped.Realm.Slug);
        Assert.Equal("Ravencrest", roundTripped.Realm.Name);
        Assert.Equal("Horde", roundTripped.Faction!.Name);
        Assert.Equal(250, roundTripped.MemberCount);
        Assert.Equal(12345, roundTripped.AchievementPoints);
    }
}
