// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Serialization;

namespace Lfm.Api.Services;

/// <summary>
/// Typed HTTP client for the Blizzard Game Data API (static namespace).
///
/// Uses client credentials (grant_type=client_credentials) rather than a
/// user access token, mirroring fetchBlizzardToken in reference-sync-blizzard.ts.
///
/// Base URL: https://{region}.api.blizzard.com/ (set in Program.cs).
/// Static-data requests append namespace=static-{region}&amp;locale=en_US.
/// Dynamic Mythic Keystone requests append namespace=dynamic-{region}&amp;locale=en_US.
/// </summary>
public interface IBlizzardGameDataClient
{
    /// <summary>
    /// Obtains a client-credentials access token from Battle.net OAuth.
    /// </summary>
    Task<string> GetClientCredentialsTokenAsync(CancellationToken ct);

    /// <summary>
    /// Fetches the playable-class index.
    /// </summary>
    Task<BlizzardPlayableClassIndex> GetPlayableClassIndexAsync(string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a single playable-class detail document.
    /// </summary>
    Task<BlizzardPlayableClassDetail> GetPlayableClassAsync(int classId, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches the playable-specialization index.
    /// </summary>
    Task<BlizzardPlayableSpecIndex> GetPlayableSpecIndexAsync(string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a single playable-specialization detail document.
    /// </summary>
    Task<BlizzardPlayableSpecDetail> GetPlayableSpecAsync(int specId, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches the media (icon URL) for a single playable-specialization.
    /// </summary>
    Task<BlizzardMediaAssets> GetPlayableSpecMediaAsync(int specId, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches the journal-expansion index — the canonical ordered list of
    /// WoW expansions. Used to populate the expansion selector on the
    /// create-run form. Each entry carries only <c>Id</c> and <c>Name</c>;
    /// per-expansion detail carries the non-M+ dungeon / raid membership.
    /// </summary>
    Task<BlizzardJournalExpansionIndex> GetJournalExpansionIndexAsync(string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a single journal-expansion detail document, including Blizzard's
    /// dungeon and raid membership lists for that expansion.
    /// </summary>
    Task<BlizzardJournalExpansionDetail> GetJournalExpansionAsync(
        int expansionId,
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// Fetches the Mythic Keystone season index from the dynamic namespace.
    /// </summary>
    Task<BlizzardMythicKeystoneSeasonIndex> GetMythicKeystoneSeasonIndexAsync(
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// Fetches one Mythic Keystone season detail document from the dynamic namespace.
    /// Blizzard's current response may omit dungeon membership; callers must
    /// tolerate <see cref="BlizzardMythicKeystoneSeasonDetail.Dungeons"/> being null.
    /// </summary>
    Task<BlizzardMythicKeystoneSeasonDetail> GetMythicKeystoneSeasonAsync(
        int seasonId,
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// Fetches the connected-realm index from the dynamic namespace. The
    /// Mythic Keystone leaderboard index is scoped by connected realm.
    /// </summary>
    Task<BlizzardConnectedRealmIndex> GetConnectedRealmIndexAsync(
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// Fetches the current Mythic Keystone leaderboard index for a connected realm
    /// from the dynamic namespace.
    /// </summary>
    Task<BlizzardMythicKeystoneLeaderboardIndex> GetMythicKeystoneLeaderboardsIndexAsync(
        int connectedRealmId,
        string accessToken,
        CancellationToken ct);

    /// <summary>
    /// Fetches the journal-instance index.
    /// </summary>
    Task<BlizzardJournalInstanceIndex> GetJournalInstanceIndexAsync(string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a single journal-instance detail document.
    /// </summary>
    Task<BlizzardJournalInstanceDetail> GetJournalInstanceAsync(int instanceId, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches the media (tile / image URLs) for a single journal-instance.
    /// </summary>
    Task<BlizzardMediaAssets> GetJournalInstanceMediaAsync(int instanceId, string accessToken, CancellationToken ct);
}

// ---------------------------------------------------------------------------
// Response DTOs — minimal: only fields consumed by the sync service.
// [JsonPropertyName] is required for Blizzard's snake_case fields; the simple
// camelCase fields work via PropertyNameCaseInsensitive = true in BlizzardGameDataClient.
// ---------------------------------------------------------------------------

public sealed record BlizzardIndexEntry(int Id, string Name);

public sealed record BlizzardPlayableClassIndex(IReadOnlyList<BlizzardIndexEntry> Classes);

public sealed record BlizzardPlayableClassDetail(int Id, string Name);

/// <summary>Blizzard playable-specialization index response.</summary>
public sealed record BlizzardPlayableSpecIndex(
    [property: JsonPropertyName("character_specializations")]
    IReadOnlyList<BlizzardIndexEntry> CharacterSpecializations);

public sealed record BlizzardPlayableSpecClassRef(int Id, string Name);

public sealed record BlizzardPlayableSpecRoleRef(string Type, string Name);

public sealed record BlizzardPlayableSpecDetail(
    int Id,
    string Name,
    [property: JsonPropertyName("playable_class")] BlizzardPlayableSpecClassRef PlayableClass,
    BlizzardPlayableSpecRoleRef Role);

public sealed record BlizzardMediaAsset(string Key, string Value);

public sealed record BlizzardMediaAssets(IReadOnlyList<BlizzardMediaAsset>? Assets);

/// <summary>Blizzard journal-expansion index response. The wire field is
/// <c>tiers</c>, not <c>expansions</c>.</summary>
public sealed record BlizzardJournalExpansionIndex(
    [property: JsonPropertyName("tiers")]
    IReadOnlyList<BlizzardIndexEntry> Tiers);

public sealed record BlizzardJournalExpansionDetail(
    int Id,
    string Name,
    IReadOnlyList<BlizzardIndexEntry>? Dungeons,
    IReadOnlyList<BlizzardIndexEntry>? Raids);

public sealed record BlizzardMythicKeystoneSeasonReference(int Id);

public sealed record BlizzardMythicKeystoneSeasonIndex(
    [property: JsonPropertyName("current_season")]
    BlizzardMythicKeystoneSeasonReference? CurrentSeason,
    IReadOnlyList<BlizzardMythicKeystoneSeasonReference> Seasons);

public sealed record BlizzardMythicKeystoneSeasonDetail(
    int Id,
    [property: JsonPropertyName("season_name")] string? SeasonName,
    IReadOnlyList<BlizzardIndexEntry>? Dungeons);

public sealed record BlizzardHrefReference(string Href);

public sealed record BlizzardConnectedRealmReference(BlizzardHrefReference Key);

public sealed record BlizzardConnectedRealmIndex(
    [property: JsonPropertyName("connected_realms")]
    IReadOnlyList<BlizzardConnectedRealmReference> ConnectedRealms);

public sealed record BlizzardMythicKeystoneLeaderboardIndex(
    [property: JsonPropertyName("current_leaderboards")]
    IReadOnlyList<BlizzardIndexEntry>? CurrentLeaderboards);

public sealed record BlizzardJournalInstanceIndex(IReadOnlyList<BlizzardIndexEntry> Instances);

public sealed record BlizzardJournalInstanceMode(
    BlizzardJournalModeRef Mode,
    int? Players,
    [property: JsonPropertyName("is_tracked")] bool? IsTracked);

public sealed record BlizzardJournalModeRef(string Type, string Name);

public sealed record BlizzardJournalInstanceCategory(string Type);

public sealed record BlizzardJournalExpansion(int Id, string Name);

public sealed record BlizzardJournalInstanceDetail(
    int Id,
    string Name,
    BlizzardJournalInstanceCategory? Category,
    BlizzardJournalExpansion? Expansion,
    [property: JsonPropertyName("minimum_level")] int? MinimumLevel,
    IReadOnlyList<BlizzardJournalInstanceMode>? Modes,
    BlizzardMediaAssets? Media);
