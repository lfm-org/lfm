// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/data/wow/guild/{realm}/{name}</c> response (HTTP wire shape).
/// STJ-only snake_case mapping. Never stored directly in Cosmos —
/// translate via <see cref="Lfm.Api.Services.Blizzard.BlizzardModelTranslator.ToStored(GuildProfileResponse)"/>
/// before persisting.
///
/// Reserved for the upcoming guild-roster port — no .NET caller yet; the
/// TypeScript handler still owns guild roster refresh. Kept here so the
/// .NET port can adopt the wire/storage split without re-introducing the
/// dual-roled record shape.
/// </summary>
public sealed record GuildProfileResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("realm")] GuildProfileRealmResponse Realm,
    [property: JsonPropertyName("faction")] GuildProfileFactionResponse? Faction = null,
    [property: JsonPropertyName("member_count")] int? MemberCount = null,
    [property: JsonPropertyName("achievement_points")] int? AchievementPoints = null);

public sealed record GuildProfileRealmResponse(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record GuildProfileFactionResponse(
    [property: JsonPropertyName("name")] string? Name = null);
