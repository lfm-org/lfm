// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/profile/wow/character/{realm}/{name}/specializations</c> response
/// (HTTP wire shape). STJ-only snake_case mapping. Never stored directly in Cosmos.
/// </summary>
public sealed record CharacterSpecializationsResponse(
    [property: JsonPropertyName("active_specialization")] SpecializationRefResponse? ActiveSpecialization = null,
    [property: JsonPropertyName("specializations")] IReadOnlyList<SpecializationEntryResponse>? Specializations = null);

public sealed record SpecializationEntryResponse(
    [property: JsonPropertyName("specialization")] SpecializationRefResponse Specialization);

public sealed record SpecializationRefResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);
