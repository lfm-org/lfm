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
        var json = """{ "name": { "en_US": "Horde", "de_DE": "Horde", "fr_FR": "Horde" } }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Equal("Horde", result!.Name);
    }

    [Fact]
    public void Reads_localized_object_without_en_US_picks_first_non_empty_value()
    {
        var json = """{ "name": { "de_DE": "Horde", "fr_FR": "Horde" } }""";
        var result = JsonConvert.DeserializeObject<Holder>(json, CosmosLikeSettings);
        Assert.Equal("Horde", result!.Name);
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
        Assert.Contains("\"name\":\"Horde\"", json);
    }

    // ------------------------------------------------------------------
    // End-to-end: GuildDocument deserialization matches the production path
    // ------------------------------------------------------------------

    [Fact]
    public void GuildDocument_deserializes_with_localized_faction_name()
    {
        // Shape written by Blizzard's profile endpoint when called without
        // locale=en_US — this is the exact payload that triggers the prod bug.
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
              "name": { "en_US": "Horde", "de_DE": "Horde", "fr_FR": "Horde" }
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

    // Record used only by the converter unit tests above.
    private sealed record Holder(
        [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);
}
