// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Guild;

/// <summary>
/// Guild info nested inside GuildDto; null when the user is not in a guild
/// or the guild document has not been bootstrapped yet.
/// Mirrors the <c>guild</c> field in the TypeScript <c>GuildHomeView</c>.
/// </summary>
public sealed record GuildInfoDto(
    int Id,
    string Name,
    string? Slogan,
    string RealmSlug,
    string RealmName,
    string? FactionName,
    int? MemberCount,
    int? AchievementPoints,
    int? SyncedMemberCount,
    int? RankCount,
    string? CrestEmblemUrl,
    string? CrestBorderUrl);

/// <summary>
/// Setup / initialisation status of the guild.
/// Mirrors the <c>setup</c> field in the TypeScript <c>GuildHomeView</c>.
/// </summary>
public sealed record GuildSetupDto(
    bool IsInitialized,
    bool RequiresSetup,
    bool RankDataFresh,
    string? RankDataFetchedAt,
    string Timezone,
    string Locale);

/// <summary>
/// Per-rank permission entry.
/// Mirrors the <c>rankPermissions</c> entries in the TypeScript <c>GuildHomeView</c>.
/// </summary>
public sealed record GuildRankPermissionDto(
    int Rank,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns);

/// <summary>
/// Settings visible to a guild editor.
/// Null when the current user does not have edit rights.
/// </summary>
public sealed record GuildSettingsDto(
    IReadOnlyList<GuildRankPermissionDto> RankPermissions);

/// <summary>
/// Guild editor context.
/// </summary>
public sealed record GuildEditorDto(
    bool CanEdit,
    string Mode);

/// <summary>
/// Effective permission set for the current raider within the guild.
/// Mirrors the <c>memberPermissions</c> field in the TypeScript <c>GuildHomeView</c>.
/// </summary>
public sealed record GuildMemberPermissionsDto(
    int? MatchedRank,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns,
    bool RankDataFresh);

/// <summary>
/// Response body for GET /api/guild.
/// Mirrors the TypeScript <c>GuildHomeView</c> returned by <c>loadCurrentGuildHome</c>.
/// </summary>
public sealed record GuildDto(
    GuildInfoDto? Guild,
    GuildSetupDto Setup,
    GuildSettingsDto? Settings,
    GuildEditorDto Editor,
    GuildMemberPermissionsDto MemberPermissions);
