// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Guild;

/// <summary>
/// A single rank permission entry in an UpdateGuildRequest.
/// </summary>
public sealed record UpdateGuildRankPermission(
    int Rank,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns);

/// <summary>
/// Request body for PATCH /api/guild.
/// Fields mirror the TypeScript <c>parseGuildSettingsInput</c> function in
/// <c>functions/src/lib/guild/settings.ts</c>.
/// </summary>
public sealed record UpdateGuildRequest(
    string? Timezone,
    string? Locale,
    string? Slogan,
    IReadOnlyList<UpdateGuildRankPermission>? RankPermissions);
