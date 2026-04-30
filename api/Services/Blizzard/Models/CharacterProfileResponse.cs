// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/profile/wow/character/{realm}/{name}</c> response (HTTP wire shape).
/// STJ-only snake_case mapping from Blizzard's response payload. Never stored
/// directly in Cosmos.
/// </summary>
public sealed record CharacterProfileResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("character_class")] NamedRefResponse? CharacterClass = null,
    [property: JsonPropertyName("race")] NamedRefResponse? Race = null,
    [property: JsonPropertyName("realm")] RealmRefResponse? Realm = null,
    [property: JsonPropertyName("guild")] CharacterGuildRefResponse? Guild = null);

public sealed record CharacterGuildRefResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);
