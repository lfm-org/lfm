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
    // blizzardRosterRaw: embedded JSON object — modelled as opaque string to
    // avoid pulling in the full Blizzard roster type in this iteration.
    // The service layer deserialises it when needed.
    BlizzardGuildRosterRaw? BlizzardRosterRaw = null,
    BlizzardGuildProfileRaw? BlizzardProfileRaw = null);

/// <summary>
/// Blizzard guild roster member character (minimal fields needed for rank matching).
/// </summary>
public sealed record BlizzardGuildRosterMemberCharacter(
    string Name,
    BlizzardGuildRosterRealm Realm,
    int? Id = null);

/// <summary>Realm reference embedded in a Blizzard guild roster member.</summary>
public sealed record BlizzardGuildRosterRealm(string Slug);

/// <summary>Single member entry in a Blizzard guild roster response.</summary>
public sealed record BlizzardGuildRosterMember(
    BlizzardGuildRosterMemberCharacter Character,
    int Rank);

/// <summary>
/// Blizzard guild roster response as stored inside the guild document.
/// Only the fields used by GuildPermissions (rank matching) are modelled here.
/// </summary>
public sealed record BlizzardGuildRosterRaw(
    IReadOnlyList<BlizzardGuildRosterMember>? Members = null);

/// <summary>Faction reference in a Blizzard guild profile response.</summary>
public sealed record BlizzardGuildProfileFaction(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

/// <summary>Realm reference in a Blizzard guild profile response.</summary>
public sealed record BlizzardGuildProfileRealm(
    string Slug,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? Name = null);

/// <summary>
/// Blizzard guild profile as stored inside the guild document.
/// Only the fields used for building the GuildDto are modelled here.
/// </summary>
public sealed record BlizzardGuildProfileRaw(
    [property: JsonConverter(typeof(LocalizedStringConverter))] string Name,
    BlizzardGuildProfileRealm Realm,
    BlizzardGuildProfileFaction? Faction = null,
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
    /// </summary>
    Task UpsertAsync(GuildDocument doc, CancellationToken ct);
}
