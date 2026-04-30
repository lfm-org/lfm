// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services.Blizzard.Models;

/// <summary>
/// Blizzard <c>/profile/user/wow</c> response (HTTP wire shape).
/// STJ-only snake_case mapping from Blizzard's response payload. Never stored
/// directly in Cosmos — translate via <see cref="BlizzardModelTranslator"/> first.
/// </summary>
public sealed record AccountProfileSummaryResponse(
    [property: JsonPropertyName("wow_accounts")] IReadOnlyList<WowAccountResponse>? WowAccounts = null);

public sealed record WowAccountResponse(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("characters")] IReadOnlyList<AccountCharacterResponse>? Characters = null);

public sealed record AccountCharacterResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("realm")] RealmRefResponse Realm,
    [property: JsonPropertyName("playable_class")] NamedRefResponse? PlayableClass = null);

public sealed record RealmRefResponse(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record NamedRefResponse(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);
