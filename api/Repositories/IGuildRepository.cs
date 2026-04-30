// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Newtonsoft.Json;
using Lfm.Api.Serialization;

namespace Lfm.Api.Repositories;

// ---------------------------------------------------------------------------
// Guild rank permission as stored in the Cosmos guild document.
// ---------------------------------------------------------------------------

public sealed record GuildRankPermission(
    int Rank,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns = false);

// ---------------------------------------------------------------------------
// Guild setup sub-document.
// ---------------------------------------------------------------------------

public sealed record GuildSetup(
    string? InitializedAt = null,
    string? Timezone = null,
    string? Locale = null);

/// <summary>
/// Guild document as stored in the Cosmos "guilds" container.
/// Partition key: /id (the guild ID as a string).
/// Only the fields needed for the current set of ported endpoints are modelled here.
/// Additional fields will be added incrementally as further endpoints are ported.
/// </summary>
public sealed record GuildDocument(
    string Id,
    int GuildId,
    string RealmSlug,
    string? Slogan = null,
    string? BlizzardRosterFetchedAt = null,
    string? BlizzardProfileFetchedAt = null,
    string? CrestEmblemUrl = null,
    string? CrestBorderUrl = null,
    IReadOnlyList<GuildRankPermission>? RankPermissions = null,
    GuildSetup? Setup = null,
    string? LastOverrideBy = null,
    string? LastOverrideAt = null,
    // blizzardRosterRaw / blizzardProfileRaw: cached Blizzard responses,
    // translated from Blizzard wire shapes via BlizzardModelTranslator.
    StoredGuildRoster? BlizzardRosterRaw = null,
    StoredGuildProfile? BlizzardProfileRaw = null,
    // Cosmos _etag — populated by the repository on read, used by PATCH /guild
    // to honor client-supplied If-Match headers for optimistic concurrency.
    [property: System.Text.Json.Serialization.JsonPropertyName("_etag")] string? ETag = null);

// ---------------------------------------------------------------------------
// Stored Blizzard guild roster — embedded inside the guild Cosmos document.
// Cosmos keys are derived from .NET property names via CamelCase contract
// resolver. Renaming any property here changes the JSON key written to Cosmos
// and breaks reading existing documents.
//
// Resolves SD-S-5 from docs/superpowersreviews/2026-04-29-software-design-deep-review.md.
// ---------------------------------------------------------------------------

/// <summary>
/// Stored Blizzard guild roster member character (minimal fields needed for rank matching).
/// </summary>
public sealed record StoredGuildRosterMemberCharacter(
    string Name,
    StoredGuildRosterRealm Realm,
    int? Id = null);

/// <summary>Realm reference embedded in a stored Blizzard guild roster member.</summary>
public sealed record StoredGuildRosterRealm(string Slug);

/// <summary>Single member entry in a stored Blizzard guild roster.</summary>
public sealed record StoredGuildRosterMember(
    StoredGuildRosterMemberCharacter Character,
    int Rank);

/// <summary>
/// Blizzard guild roster as stored inside the guild Cosmos document.
/// Only the fields used by GuildPermissions (rank matching) are modelled here.
/// </summary>
public sealed record StoredGuildRoster(
    IReadOnlyList<StoredGuildRosterMember>? Members = null);

/// <summary>Faction reference in a stored Blizzard guild profile.</summary>
public sealed record StoredGuildProfileFaction(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

/// <summary>Realm reference in a stored Blizzard guild profile.</summary>
public sealed record StoredGuildProfileRealm(
    string Slug,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

/// <summary>
/// Blizzard guild profile as stored inside the guild Cosmos document.
/// Only the fields used for building the GuildDto are modelled here.
/// </summary>
public sealed record StoredGuildProfile(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    StoredGuildProfileRealm Realm,
    StoredGuildProfileFaction? Faction = null,
    int? MemberCount = null,
    int? AchievementPoints = null);

public interface IGuildRepository
{
    /// <summary>
    /// Point-read the guild document by its string ID (which is also the partition key).
    /// Returns null when the document does not exist.
    /// </summary>
    Task<GuildDocument?> GetAsync(string guildId, CancellationToken ct);

    /// <summary>
    /// Upserts a guild document. Partition key is the document's Id.
    /// Does not check the ETag — used by internal flows (roster refresh, admin
    /// override timestamps) that reconcile their own state.
    /// </summary>
    Task UpsertAsync(GuildDocument doc, CancellationToken ct);

    /// <summary>
    /// Replaces a guild document under an optimistic-concurrency guard. When
    /// Cosmos reports 412 Precondition Failed the repository surfaces
    /// <see cref="ConcurrencyConflictException"/> so the caller can translate
    /// to a client-visible 412 problem+json. Used by PATCH /api/guild to
    /// honor the caller's <c>If-Match</c> header.
    /// </summary>
    Task<GuildDocument> ReplaceAsync(GuildDocument doc, string ifMatchEtag, CancellationToken ct);
}
