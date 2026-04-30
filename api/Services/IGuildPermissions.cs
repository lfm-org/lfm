// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

/// <summary>
/// Resolves guild-level permissions for the authenticated raider.
/// Mirrors the TypeScript permission logic in
/// <c>functions/src/lib/guild/context.ts</c> (<c>resolveGuildEditor</c>).
/// </summary>
public interface IGuildPermissions
{
    /// <summary>
    /// Returns true if the raider is a guild admin (rank 0 in the Blizzard
    /// roster for their guild), mirroring the TypeScript <c>resolveGuildEditor</c>
    /// check: <c>canEdit = bestRank === 0</c>.
    /// Guild is derived from the raider's selected character via
    /// <c>GuildResolver.FromRaider</c>.
    /// </summary>
    Task<bool> IsAdminAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Returns true if the raider's matched guild rank has the
    /// <c>canCreateGuildRuns</c> permission, mirroring the TypeScript
    /// <c>getEffectiveGuildPermissions(...).canCreateGuildRuns</c> check in
    /// <c>functions/src/lib/guild-permissions.ts</c>.
    /// Returns false when the raider has no guild, the roster is not fresh,
    /// the raider is not in the roster, or the rank permission is not set.
    /// Guild is derived from the raider's selected character via
    /// <c>GuildResolver.FromRaider</c>.
    /// </summary>
    Task<bool> CanCreateGuildRunsAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Returns true if the raider's matched guild rank has the
    /// <c>canSignupGuildRuns</c> permission, mirroring the TypeScript
    /// <c>getEffectiveGuildPermissions(...).canSignupGuildRuns</c> check in
    /// <c>functions/src/lib/guild-permissions.ts</c>.
    /// Returns false when the raider has no guild, the roster is not fresh,
    /// the raider is not in the roster, or the rank permission is not set.
    /// Default: all ranks can sign up for guild runs (canSignupGuildRuns defaults to true).
    /// Guild is derived from the raider's selected character via
    /// <c>GuildResolver.FromRaider</c>.
    /// </summary>
    Task<bool> CanSignupGuildRunsAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Returns true if the raider's matched guild rank has the
    /// <c>canDeleteGuildRuns</c> permission, mirroring the TypeScript
    /// <c>getEffectiveGuildPermissions(...).canDeleteGuildRuns</c> check in
    /// <c>functions/src/lib/guild-permissions.ts</c>.
    /// Returns false when the raider has no guild, the roster is not fresh,
    /// the raider is not in the roster, or the rank permission is not set.
    /// Default: only rank 0 (guild master) can delete guild runs.
    /// Guild is derived from the raider's selected character via
    /// <c>GuildResolver.FromRaider</c>.
    /// </summary>
    Task<bool> CanDeleteGuildRunsAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Computes <see cref="IsAdminAsync"/>, <see cref="CanCreateGuildRunsAsync"/>,
    /// <see cref="CanSignupGuildRunsAsync"/>, and <see cref="CanDeleteGuildRunsAsync"/>
    /// in a single guild-document load. Use from the guild GET path so a single
    /// response does not trigger four guild reads.
    /// </summary>
    Task<GuildEffectivePermissions> GetEffectivePermissionsAsync(RaiderDocument raider, CancellationToken ct);
}

/// <summary>
/// Combined effective permissions returned by
/// <see cref="IGuildPermissions.GetEffectivePermissionsAsync"/>. Mirrors the
/// shape consumed by the SPA's <c>GuildMemberPermissionsDto</c> + editor flag.
/// </summary>
public sealed record GuildEffectivePermissions(
    bool IsAdmin,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns)
{
    public static readonly GuildEffectivePermissions None = new(false, false, false, false);
}
