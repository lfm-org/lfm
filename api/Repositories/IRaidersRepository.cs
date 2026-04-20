// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;
using Lfm.Api.Serialization;

namespace Lfm.Api.Repositories;

// ---------------------------------------------------------------------------
// Blizzard account profile — stored verbatim as returned by the Blizzard API.
// Field names follow Blizzard's snake_case convention; STJ [JsonPropertyName]
// attributes are required because the Cosmos client is configured for camelCase
// only at the *top-level document* boundary, not for nested objects.
// ---------------------------------------------------------------------------

public sealed record BlizzardRealmRef(
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record BlizzardNamedRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record BlizzardAccountCharacter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("realm")] BlizzardRealmRef Realm,
    [property: JsonPropertyName("playable_class")] BlizzardNamedRef? PlayableClass = null);

public sealed record BlizzardWowAccount(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("characters")] IReadOnlyList<BlizzardAccountCharacter>? Characters = null);

public sealed record BlizzardAccountProfileSummary(
    [property: JsonPropertyName("wow_accounts")] IReadOnlyList<BlizzardWowAccount>? WowAccounts = null);

// ---------------------------------------------------------------------------
// Stored selected character — minimal fields required for character mapping.
// Field names match the camelCase keys the TS refresh handler writes to Cosmos.
// ---------------------------------------------------------------------------

public sealed record StoredCharacterSpecialization(
    int Id,
    [property: Newtonsoft.Json.JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

public sealed record StoredSpecializationsSummary(
    StoredCharacterSpecialization? ActiveSpecialization = null,
    IReadOnlyList<StoredSpecializationsEntry>? Specializations = null);

public sealed record StoredSpecializationsEntry(StoredCharacterSpecialization Specialization);

/// <summary>
/// A single asset entry in a Blizzard character media summary.
/// Field names match the snake_case Blizzard API response (e.g. "avatar", "main").
/// </summary>
public sealed record BlizzardCharacterMediaAsset(string Key, string Value);

/// <summary>
/// The Blizzard character media summary response.
/// Mirrors <c>BlizzardCharacterMediaSummary</c> from
/// <c>functions/src/types/blizzard.ts</c>.
/// </summary>
public sealed record BlizzardCharacterMediaSummary(
    IReadOnlyList<BlizzardCharacterMediaAsset>? Assets = null);

public sealed record StoredSelectedCharacter(
    string Id,
    string Region,
    string Realm,
    string Name,
    string? PortraitUrl = null,
    StoredSpecializationsSummary? SpecializationsSummary = null,
    BlizzardCharacterMediaSummary? MediaSummary = null,
    int? ClassId = null,
    [property: Newtonsoft.Json.JsonConverter(typeof(LocalizedStringConverter))] string? ClassName = null,
    int? Level = null,
    int? GuildId = null,
    string? GuildName = null,
    string? FetchedAt = null,
    string? ProfileFetchedAt = null,
    string? SpecsFetchedAt = null,
    string? MediaFetchedAt = null);

/// <summary>
/// Raider document as stored in the Cosmos "raiders" container.
/// Partition key: /battleNetId
/// Only the fields needed for the current set of ported endpoints are modelled here.
/// Additional fields will be added incrementally as further endpoints are ported.
/// </summary>
public sealed record RaiderDocument(
    string Id,
    string BattleNetId,
    string? SelectedCharacterId,
    string? Locale,
    // lastSeenAt: ISO-8601 timestamp updated on every login (set by battlenet-callback).
    // Used by the raider-cleanup timer to identify inactive accounts (> 90 days).
    string? LastSeenAt = null,
    // Cosmos TTL in seconds. Set by me-update (180 * 86400 = ~180 days).
    // Null means no TTL override; the container default applies.
    // Must not serialize null — Cosmos rejects "ttl": null as invalid.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Ttl = null,
    // accountProfileSummary: cached Blizzard profile response (populated by battlenet-characters-refresh).
    BlizzardAccountProfileSummary? AccountProfileSummary = null,
    // accountProfileRefreshedAt: ISO-8601 timestamp of last cooldown reset (even on 304 / not-modified).
    string? AccountProfileRefreshedAt = null,
    // accountProfileFetchedAt: ISO-8601 timestamp of last full Blizzard fetch (only updated on 200 OK).
    string? AccountProfileFetchedAt = null,
    // characters: stored selected character details (populated by raider-character flow).
    IReadOnlyList<StoredSelectedCharacter>? Characters = null,
    // portraitCache: map of "{region}-{realm}-{name}" → portrait URL (populated by portrait refresh).
    IReadOnlyDictionary<string, string>? PortraitCache = null);

// ---------------------------------------------------------------------------
// Blizzard character profile — response from /profile/wow/character/{realm}/{name}
// ---------------------------------------------------------------------------

public sealed record BlizzardCharacterGuildRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record BlizzardCharacterProfileResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("character_class")] BlizzardNamedRef? CharacterClass = null,
    [property: JsonPropertyName("race")] BlizzardNamedRef? Race = null,
    [property: JsonPropertyName("realm")] BlizzardRealmRef? Realm = null,
    [property: JsonPropertyName("guild")] BlizzardCharacterGuildRef? Guild = null);

// ---------------------------------------------------------------------------
// Blizzard character specializations — response from /profile/wow/character/{realm}/{name}/specializations
// ---------------------------------------------------------------------------

public sealed record BlizzardSpecializationRef(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name = null);

public sealed record BlizzardSpecializationEntry(
    [property: JsonPropertyName("specialization")] BlizzardSpecializationRef Specialization);

public sealed record BlizzardCharacterSpecializationsResponse(
    [property: JsonPropertyName("active_specialization")] BlizzardSpecializationRef? ActiveSpecialization = null,
    [property: JsonPropertyName("specializations")] IReadOnlyList<BlizzardSpecializationEntry>? Specializations = null);

public interface IRaidersRepository
{
    /// <summary>
    /// Point-read by battleNetId (which is both the document id and partition key).
    /// Returns null when the document does not exist.
    /// </summary>
    Task<RaiderDocument?> GetByBattleNetIdAsync(string battleNetId, CancellationToken ct);

    /// <summary>
    /// Upserts a raider document. Partition key is the document's BattleNetId.
    /// </summary>
    Task UpsertAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Deletes the raider document identified by battleNetId (which is both the
    /// document id and partition key). Treats NotFound as success (idempotent).
    /// </summary>
    Task DeleteAsync(string battleNetId, CancellationToken ct);

    /// <summary>
    /// Returns all raider documents where lastSeenAt is older than the given cutoff,
    /// or where lastSeenAt is not defined. These are candidates for cleanup.
    /// Mirrors the query in raider-cleanup.ts:
    ///   SELECT c.id, c.battleNetId FROM c WHERE c.lastSeenAt &lt; @cutoff OR NOT IS_DEFINED(c.lastSeenAt)
    /// Only id and battleNetId are projected (minimises RU cost).
    /// </summary>
    Task<IReadOnlyList<RaiderDocument>> ListExpiredAsync(string cutoff, CancellationToken ct);
}
