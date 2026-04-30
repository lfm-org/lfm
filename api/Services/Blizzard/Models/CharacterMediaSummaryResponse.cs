// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/profile/wow/character/{realm}/{name}/character-media</c> response
/// (HTTP wire shape). STJ-only snake_case mapping from Blizzard. Never stored
/// directly in Cosmos — translate via <see cref="BlizzardModelTranslator"/> first.
/// </summary>
public sealed record CharacterMediaSummaryResponse(
    [property: JsonPropertyName("assets")] IReadOnlyList<CharacterMediaAssetResponse>? Assets = null);

public sealed record CharacterMediaAssetResponse(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string Value);
