// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Guild;

/// <summary>
/// Guild info nested inside GuildDto; null when the user is not in a guild
/// or the guild document has not been bootstrapped yet.
/// Wire-only shape per docs/wire-payload-contract.md — fields the app does
/// not render (RealmSlug, AchievementPoints, SyncedMemberCount) are omitted.
/// </summary>
public sealed record GuildInfoDto(
    int Id,
    string Name,
    string? Slogan,
    string RealmName,
    string? FactionName,
    int? MemberCount,
    int? RankCount,
    string? CrestEmblemUrl,
    string? CrestBorderUrl);

/// <summary>
/// Setup / initialisation status of the guild.
/// Wire-only shape per docs/wire-payload-contract.md — RankDataFetchedAt is
/// omitted; the app reads RankDataFresh (the derived boolean), not the
/// raw timestamp.
/// </summary>
public sealed record GuildSetupDto(
    bool IsInitialized,
    bool RequiresSetup,
    bool RankDataFresh,
    string Timezone,
    string Locale);

/// <summary>
/// Per-rank permission entry.
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
    bool CanEdit);

/// <param name="CanDeleteGuildRuns">
/// Kept for peer-permission symmetry with CanCreateGuildRuns / CanSignupGuildRuns
/// (the wire-payload-contract peer-permission exception — see
/// docs/wire-payload-contract.md), even though no UI surfaces it today.
/// </param>
/// <summary>
/// Effective permission set for the current raider within the guild.
/// Wire-only shape per docs/wire-payload-contract.md — MatchedRank is
/// omitted (not rendered) and RankDataFresh is read from GuildSetupDto.
/// </summary>
public sealed record GuildMemberPermissionsDto(
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns);

/// <summary>
/// Response body for GET /api/guild.
/// </summary>
public sealed record GuildDto(
    GuildInfoDto? Guild,
    GuildSetupDto Setup,
    GuildSettingsDto? Settings,
    GuildEditorDto Editor,
    GuildMemberPermissionsDto MemberPermissions);
