// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Lfm.Api.Tests.Serialization;

/// <summary>
/// Regression coverage for the production bug where guild documents written by
/// the legacy pipeline (no <c>locale</c> query param) store Blizzard's localized
/// *object* shape at paths like <c>blizzardProfileRaw.faction.name</c>:
/// <code>{ "en_US": "Horde", "de_DE": "Horde", ... }</code>
/// The .NET reader, expecting a plain string, threw
/// <c>Newtonsoft.Json.JsonReaderException</c> on every /api/guild call.
/// </summary>
public class LocalizedStringConverterTests
{
    private static readonly JsonSerializerSettings CosmosLikeSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore,
    };

    // ------------------------------------------------------------------
    // Converter unit tests
    // ------------------------------------------------------------------

    [Fact]
    public void Reads_plain_string_value()
    {
        var json = """{ "name": "Horde" }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Equal("Horde", result!.Name);
    }

    [Fact]
    public void Reads_localized_object_prefers_en_US()
    {
        // Distinct values across locales so the en_US preference is actually
        // witnessed — identical values would pass even if another key won.
        var json = """{ "name": { "en_US": "en-value", "de_DE": "de-value", "fr_FR": "fr-value" } }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Equal("en-value", result!.Name);
    }

    [Fact]
    public void Reads_localized_object_without_en_US_picks_first_non_empty_value()
    {
        // Distinct values so the "first" claim is actually witnessed — identical
        // values would pass even if the converter walked the object in reverse.
        var json = """{ "name": { "de_DE": "de-value", "fr_FR": "fr-value" } }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Equal("de-value", result!.Name);
    }

    [Fact]
    public void Reads_null_as_null()
    {
        var json = """{ "name": null }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Null(result!.Name);
    }

    [Fact]
    public void Reads_empty_object_as_null()
    {
        var json = """{ "name": {} }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Null(result!.Name);
    }

    [Fact]
    public void Missing_field_stays_null()
    {
        var json = "{}";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Null(result!.Name);
    }

    [Fact]
    public void Writes_string_value_as_plain_string()
    {
        var holder = new Holder("Horde");
        var json = JsonConvert.SerializeObject(holder, CosmosLikeSettings);
        Assert.Equal("""{"name":"Horde"}""", json);
    }

    // ------------------------------------------------------------------
    // End-to-end: GuildDocument deserialization matches the production path
    // ------------------------------------------------------------------

    [Fact]
    public void GuildDocument_deserializes_with_localized_faction_name()
    {
        // Shape written by Blizzard's profile endpoint when called without
        // locale=en_US — this is the exact payload that triggers the prod bug.
        // Distinct per-locale values so the en_US preference is witnessed in
        // the end-to-end record wiring, not only in the converter unit test.
        var json = """
        {
          "id": "12345",
          "guildId": 12345,
          "realmSlug": "test-realm",
          "blizzardProfileRaw": {
            "name": "Test Guild",
            "realm": { "slug": "test-realm", "name": "Test Realm" },
            "faction": {
              "type": "HORDE",
              "name": { "en_US": "Horde", "de_DE": "Horde-DE", "fr_FR": "Horde-FR" }
            },
            "memberCount": 42,
            "achievementPoints": 1234
          }
        }
        """;

        var doc = JsonConvert.DeserializeObject<GuildDocument>(json, CosmosLikeSettings);

        Assert.NotNull(doc);
        Assert.NotNull(doc!.BlizzardProfileRaw);
        Assert.NotNull(doc.BlizzardProfileRaw!.Faction);
        Assert.Equal("Horde", doc.BlizzardProfileRaw.Faction!.Name);
    }

    [Fact]
    public void GuildDocument_deserializes_with_localized_realm_name()
    {
        var json = """
        {
          "id": "12345",
          "guildId": 12345,
          "realmSlug": "test-realm",
          "blizzardProfileRaw": {
            "name": "Test Guild",
            "realm": {
              "slug": "test-realm",
              "name": { "en_US": "Test Realm", "de_DE": "Test Reich" }
            }
          }
        }
        """;

        var doc = JsonConvert.DeserializeObject<GuildDocument>(json, CosmosLikeSettings);

        Assert.Equal("Test Realm", doc!.BlizzardProfileRaw!.Realm.Name);
    }

    [Fact]
    public void GuildDocument_still_deserializes_with_plain_string_faction_name()
    {
        // The locale=en_US shape must keep working.
        var json = """
        {
          "id": "12345",
          "guildId": 12345,
          "realmSlug": "test-realm",
          "blizzardProfileRaw": {
            "name": "Test Guild",
            "realm": { "slug": "test-realm", "name": "Test Realm" },
            "faction": { "type": "HORDE", "name": "Horde" }
          }
        }
        """;

        var doc = JsonConvert.DeserializeObject<GuildDocument>(json, CosmosLikeSettings);

        Assert.Equal("Horde", doc!.BlizzardProfileRaw!.Faction!.Name);
    }

    // ------------------------------------------------------------------
    // End-to-end: RunDocument deserialization (second prod failure, same
    // root cause but at runCharacters[].specName and neighbouring fields).
    // ------------------------------------------------------------------

    [Fact]
    public void RunDocument_deserializes_with_localized_character_spec_class_race_and_instance_names()
    {
        // Minimal shape of a run document as stored in Cosmos, using Blizzard's
        // no-locale localized-object shape for every Blizzard-sourced name.
        var json = """
        {
          "id": "run-1",
          "startTime": "2026-04-20T18:00:00Z",
          "signupCloseTime": "2026-04-20T17:30:00Z",
          "description": "",
          "modeKey": "NORMAL:25",
          "visibility": "GUILD",
          "creatorGuild": "Test Guild",
          "creatorGuildId": 12345,
          "instanceId": 67,
          "instanceName": { "en_US": "Gluttonous Abomination", "de_DE": "Gefrässige Abscheulichkeit" },
          "creatorBattleNetId": "bnet-1",
          "createdAt": "2026-04-20T17:00:00Z",
          "ttl": 604800,
          "runCharacters": [
            {
              "id": "rc-1",
              "characterId": "char-1",
              "characterName": "Thrall",
              "characterRealm": "test-realm",
              "characterLevel": 80,
              "characterClassId": 7,
              "characterClassName": { "en_US": "Shaman", "de_DE": "Schamane" },
              "characterRaceId": 2,
              "characterRaceName": { "en_US": "Orc", "de_DE": "Orc" },
              "raiderBattleNetId": "bnet-1",
              "desiredAttendance": "YES",
              "reviewedAttendance": "PENDING",
              "specId": 262,
              "specName": { "en_US": "Elemental", "de_DE": "Elementar" },
              "role": "DAMAGE"
            }
          ]
        }
        """;

        var doc = JsonConvert.DeserializeObject<RunDocument>(json, CosmosLikeSettings);

        Assert.NotNull(doc);
        Assert.Equal("Gluttonous Abomination", doc!.InstanceName);
        var character = Assert.Single(doc.RunCharacters);
        Assert.Equal("Shaman", character.CharacterClassName);
        Assert.Equal("Orc", character.CharacterRaceName);
        Assert.Equal("Elemental", character.SpecName);
    }

    // ------------------------------------------------------------------
    // End-to-end: RaiderDocument deserialization — the raider doc is read
    // on nearly every authenticated endpoint, so any localized-object
    // payload buried in it would take the whole API down.
    // ------------------------------------------------------------------

    [Fact]
    public void RaiderDocument_deserializes_with_localized_stored_character_class_and_spec_names()
    {
        // The raider doc's StoredSelectedCharacter.ClassName and its nested
        // SpecializationsSummary.ActiveSpecialization.Name come from legacy
        // Blizzard profile fetches; either may have been stored as a
        // localized object. The live Blizzard HTTP path uses STJ and always
        // requests locale=en_US, so BlizzardAccountProfileSummary is out of
        // scope here.
        var json = """
        {
          "id": "bnet-1",
          "battleNetId": "bnet-1",
          "selectedCharacterId": "char-1",
          "locale": null,
          "characters": [
            {
              "id": "char-1",
              "region": "eu",
              "realm": "test-realm",
              "name": "Thrall",
              "classId": 7,
              "className": { "en_US": "Shaman", "de_DE": "Schamane" },
              "specializationsSummary": {
                "activeSpecialization": {
                  "id": 262,
                  "name": { "en_US": "Elemental", "de_DE": "Elementar" }
                }
              }
            }
          ]
        }
        """;

        var doc = JsonConvert.DeserializeObject<RaiderDocument>(json, CosmosLikeSettings);

        Assert.NotNull(doc);
        var stored = Assert.Single(doc!.Characters!);
        Assert.Equal("Shaman", stored.ClassName);
        Assert.Equal("Elemental", stored.SpecializationsSummary!.ActiveSpecialization!.Name);
    }

    // ------------------------------------------------------------------
    // End-to-end: reference-data containers (instances, specializations)
    // ------------------------------------------------------------------

    [Fact]
    public void InstanceDocument_deserializes_with_localized_name()
    {
        var json = """
        {
          "id": "67:NORMAL:25",
          "instanceId": "67",
          "name": { "en_US": "Icecrown Citadel", "de_DE": "Eiskronenzitadelle" },
          "modeKey": "NORMAL:25",
          "expansion": "WRATH_OF_THE_LICH_KING"
        }
        """;

        var doc = JsonConvert.DeserializeObject<InstanceDocument>(json, CosmosLikeSettings);

        Assert.Equal("Icecrown Citadel", doc!.Name);
    }

    [Fact]
    public void SpecializationDocument_deserializes_with_localized_name()
    {
        var json = """
        {
          "id": "262",
          "specId": 262,
          "name": { "en_US": "Elemental", "de_DE": "Elementar" },
          "classId": 7,
          "role": "DAMAGE",
          "iconUrl": null
        }
        """;

        var doc = JsonConvert.DeserializeObject<SpecializationDocument>(json, CosmosLikeSettings);

        Assert.Equal("Elemental", doc!.Name);
    }

    // Record used only by the converter unit tests above.
    private sealed record Holder(
        [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);
}
