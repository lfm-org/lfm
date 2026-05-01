// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

/// <summary>
/// Checks whether the raider holds rank 0 (guild master) in their guild's
/// Blizzard roster, mirroring the TypeScript <c>resolveGuildEditor</c> logic in
/// <c>functions/src/lib/guild/context.ts</c>.
/// Guild is derived from the raider's selected character via
/// <c>GuildResolver.FromRaider</c>.
/// </summary>
public sealed class GuildPermissions(IGuildRepository guildRepo) : IGuildPermissions
{
    public async Task<bool> IsAdminAsync(RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return false;

        var guild = await guildRepo.GetAsync(guildId, ct);

        return GuildRosterMatcher.BestRank(guild?.BlizzardRosterRaw, raider.Characters) == 0;
    }

    public async Task<bool> CanCreateGuildRunsAsync(RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return false;

        var guild = await guildRepo.GetAsync(guildId, ct);

        if (guild is null || !GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        // Mirrors resolveMatchedGuildRanks + Math.min(...matchedRanks).
        var bestRank = GuildRosterMatcher.BestRank(guild.BlizzardRosterRaw, raider.Characters);
        if (bestRank is null) return false;

        // Look up rank permission entry; default canCreateGuildRuns to false.
        // Mirrors: permission?.canCreateGuildRuns ?? false.
        var rankPermissions = guild.RankPermissions;
        if (rankPermissions is not null)
        {
            var perm = rankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
            if (perm is not null)
                return perm.CanCreateGuildRuns;
        }

        // No stored permission entry → fall back to default (rank 0 can create, others cannot).
        return bestRank.Value == 0;
    }

    public async Task<bool> CanSignupGuildRunsAsync(RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return false;

        var guild = await guildRepo.GetAsync(guildId, ct);

        if (guild is null || !GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        var bestRank = GuildRosterMatcher.BestRank(guild.BlizzardRosterRaw, raider.Characters);
        if (bestRank is null) return false;

        // Look up rank permission entry; default canSignupGuildRuns to true for all ranks.
        // Mirrors: permission?.canSignupGuildRuns ?? true.
        var rankPermissions = guild.RankPermissions;
        if (rankPermissions is not null)
        {
            var perm = rankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
            if (perm is not null)
                return perm.CanSignupGuildRuns;
        }

        // No stored permission entry → fall back to default (all ranks can sign up).
        return true;
    }

    public async Task<bool> CanDeleteGuildRunsAsync(RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return false;

        var guild = await guildRepo.GetAsync(guildId, ct);

        if (guild is null || !GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        var bestRank = GuildRosterMatcher.BestRank(guild.BlizzardRosterRaw, raider.Characters);
        if (bestRank is null) return false;

        // Look up rank permission entry; default canDeleteGuildRuns to (rank 0 only).
        // Mirrors: permission?.canDeleteGuildRuns ?? (rank === 0).
        var rankPermissions = guild.RankPermissions;
        if (rankPermissions is not null)
        {
            var perm = rankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
            if (perm is not null)
                return perm.CanDeleteGuildRuns;
        }

        // No stored permission entry → fall back to default (only rank 0 can delete).
        return bestRank.Value == 0;
    }

    public async Task<GuildEffectivePermissions> GetEffectivePermissionsAsync(
        RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return GuildEffectivePermissions.None;

        var guild = await guildRepo.GetAsync(guildId, ct);
        if (guild?.BlizzardRosterRaw?.Members is null) return GuildEffectivePermissions.None;

        var bestRank = GuildRosterMatcher.BestRank(guild.BlizzardRosterRaw, raider.Characters);
        var isAdmin = bestRank == 0;

        // Roster-freshness check matches CanCreate/Signup/DeleteGuildRunsAsync —
        // 1-hour TTL after BlizzardRosterFetchedAt.
        var rosterFresh = GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt);

        if (!rosterFresh || bestRank is null)
            return new GuildEffectivePermissions(isAdmin, false, false, false);

        var perm = guild.RankPermissions?.FirstOrDefault(rp => rp.Rank == bestRank.Value);
        var canCreate = perm?.CanCreateGuildRuns ?? bestRank.Value == 0;
        var canSignup = perm?.CanSignupGuildRuns ?? true;
        var canDelete = perm?.CanDeleteGuildRuns ?? bestRank.Value == 0;

        return new GuildEffectivePermissions(isAdmin, canCreate, canSignup, canDelete);
    }
}
