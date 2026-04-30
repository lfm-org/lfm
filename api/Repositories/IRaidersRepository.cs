// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;
using Lfm.Api.Serialization;

namespace Lfm.Api.Repositories;

// ---------------------------------------------------------------------------
// Stored Blizzard account profile — embedded inside the raider Cosmos document.
// Cosmos keys are derived from .NET property names via CamelCase contract
// resolver (see Program.cs CosmosClientOptions). Renaming any property here
// changes the JSON key written to Cosmos and breaks reading existing documents.
//
// These types were previously dual-roled as Blizzard HTTP wire models. The
// HTTP wire shapes now live in api/Services/Blizzard/Models/ and a translator
// converts wire → stored at the Blizzard adapter boundary.
// Resolves SD-S-5 from docs/superpowersreviews/2026-04-29-software-design-deep-review.md.
// ---------------------------------------------------------------------------

public sealed record StoredBlizzardRealmRef(
    string Slug,
    string? Name = null);

public sealed record StoredBlizzardNamedRef(
    int Id,
    string? Name = null);

public sealed record StoredBlizzardAccountCharacter(
    string Name,
    int Level,
    StoredBlizzardRealmRef Realm,
    StoredBlizzardNamedRef? PlayableClass = null);

public sealed record StoredBlizzardWowAccount(
    int? Id,
    IReadOnlyList<StoredBlizzardAccountCharacter>? Characters = null);

public sealed record StoredBlizzardAccountProfile(
    IReadOnlyList<StoredBlizzardWowAccount>? WowAccounts = null);

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
/// A single asset entry in a Blizzard character media summary, as stored in
/// Cosmos. Field names round-trip through Newtonsoft to camelCase ("key", "value").
/// </summary>
public sealed record StoredBlizzardCharacterMediaAsset(string Key, string Value);

/// <summary>
/// The Blizzard character media summary as stored in Cosmos (inside
/// <see cref="StoredSelectedCharacter.MediaSummary"/>). Round-trips through
/// Newtonsoft + camelCase contract resolver.
/// </summary>
public sealed record StoredBlizzardCharacterMedia(
    IReadOnlyList<StoredBlizzardCharacterMediaAsset>? Assets = null);

public sealed record StoredSelectedCharacter(
    string Id,
    string Region,
    string Realm,
    string Name,
    string? PortraitUrl = null,
    StoredSpecializationsSummary? SpecializationsSummary = null,
    StoredBlizzardCharacterMedia? MediaSummary = null,
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
    StoredBlizzardAccountProfile? AccountProfileSummary = null,
    // accountProfileRefreshedAt: ISO-8601 timestamp of last cooldown reset (even on 304 / not-modified).
    string? AccountProfileRefreshedAt = null,
    // accountProfileFetchedAt: ISO-8601 timestamp of last full Blizzard fetch (only updated on 200 OK).
    string? AccountProfileFetchedAt = null,
    // characters: stored selected character details (populated by raider-character flow).
    IReadOnlyList<StoredSelectedCharacter>? Characters = null,
    // portraitCache: map of "{region}-{realm}-{name}" → portrait URL (populated by portrait refresh).
    IReadOnlyDictionary<string, string>? PortraitCache = null,
    // Cosmos _etag — populated by the repository on read, used by PATCH /me to
    // honor client-supplied If-Match headers for optimistic concurrency.
    [property: JsonPropertyName("_etag")] string? ETag = null);

public interface IRaidersRepository
{
    /// <summary>
    /// Point-read by battleNetId (which is both the document id and partition key).
    /// Returns null when the document does not exist.
    /// </summary>
    Task<RaiderDocument?> GetByBattleNetIdAsync(string battleNetId, CancellationToken ct);

    /// <summary>
    /// Upserts a raider document. Partition key is the document's BattleNetId.
    /// Does not check the ETag — used by internal flows (login, character refresh,
    /// portrait cache) that reconcile their own state.
    /// </summary>
    Task UpsertAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Replaces a raider document under an optimistic-concurrency guard. When
    /// Cosmos reports 412 Precondition Failed the repository surfaces
    /// <see cref="ConcurrencyConflictException"/> so the caller can translate
    /// to a client-visible 412 problem+json. Used by PATCH /api/me to honor
    /// the caller's <c>If-Match</c> header.
    /// </summary>
    Task<RaiderDocument> ReplaceAsync(RaiderDocument raider, string ifMatchEtag, CancellationToken ct);

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
