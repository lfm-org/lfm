// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/data/wow/guild/{realm}/{name}/roster</c> response (HTTP wire shape).
/// STJ-only snake_case mapping. Never stored directly in Cosmos —
/// translate via <see cref="Lfm.Api.Services.Blizzard.BlizzardModelTranslator.ToStored(GuildRosterResponse)"/>
/// before persisting.
///
/// Reserved for the upcoming guild-roster port — no .NET caller yet; the
/// TypeScript handler still owns guild roster refresh. Kept here so the
/// .NET port can adopt the wire/storage split without re-introducing the
/// dual-roled record shape.
/// </summary>
public sealed record GuildRosterResponse(
    [property: JsonPropertyName("members")] IReadOnlyList<GuildRosterMemberResponse>? Members = null);

public sealed record GuildRosterMemberResponse(
    [property: JsonPropertyName("character")] GuildRosterMemberCharacterResponse Character,
    [property: JsonPropertyName("rank")] int Rank);

public sealed record GuildRosterMemberCharacterResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("realm")] GuildRosterRealmResponse Realm,
    [property: JsonPropertyName("id")] int? Id = null);

public sealed record GuildRosterRealmResponse(
    [property: JsonPropertyName("slug")] string Slug);
