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

        if (guild?.BlizzardRosterRaw?.Members is null) return false;

        // Build lookup map mirroring resolveMatchedGuildRanks in
        // functions/src/lib/guild-member-match.ts.
        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;

        // Walk stored characters — return true as soon as we find a rank-0 match.
        if (raider.Characters is null) return false;
        foreach (var character in raider.Characters)
        {
            // Match the stored character against the guild roster by realm-slug + name.
            var key = $"{character.Realm}:{character.Name}";
            if (rankByKey.TryGetValue(key, out var rank) && rank == 0) return true;
        }

        return false;
    }

    public async Task<bool> CanCreateGuildRunsAsync(RaiderDocument raider, CancellationToken ct)
    {
        var (guildId, _) = GuildResolver.FromRaider(raider);
        if (guildId is null) return false;

        var guild = await guildRepo.GetAsync(guildId, ct);

        // Mirrors: getEffectiveGuildPermissions — returns false when roster is absent or stale.
        if (guild?.BlizzardRosterRaw?.Members is null) return false;

        // Roster freshness check: mirrors isGuildRosterFresh (TTL = 1 hour).
        if (guild.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        if (DateTimeOffset.UtcNow - fetchedAt >= TimeSpan.FromHours(1)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        // Mirrors resolveMatchedGuildRanks + Math.min(...matchedRanks).
        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;

        int? bestRank = null;
        if (raider.Characters is not null)
        {
            foreach (var character in raider.Characters)
            {
                var key = $"{character.Realm}:{character.Name}";
                if (rankByKey.TryGetValue(key, out var rank))
                {
                    if (bestRank is null || rank < bestRank)
                        bestRank = rank;
                }
            }
        }

        if (bestRank is null) return false;

        // Look up rank permission entry; default canCreateGuildRuns to false.
        // Mirrors: permission?.canCreateGuildRuns ?? false.
        if (guild.RankPermissions is not null)
        {
            var perm = guild.RankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
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

        // Mirrors: getEffectiveGuildPermissions — returns false when roster is absent or stale.
        if (guild?.BlizzardRosterRaw?.Members is null) return false;

        // Roster freshness check: mirrors isGuildRosterFresh (TTL = 1 hour).
        if (guild.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        if (DateTimeOffset.UtcNow - fetchedAt >= TimeSpan.FromHours(1)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;

        int? bestRank = null;
        if (raider.Characters is not null)
        {
            foreach (var character in raider.Characters)
            {
                var key = $"{character.Realm}:{character.Name}";
                if (rankByKey.TryGetValue(key, out var rank))
                {
                    if (bestRank is null || rank < bestRank)
                        bestRank = rank;
                }
            }
        }

        if (bestRank is null) return false;

        // Look up rank permission entry; default canSignupGuildRuns to true for all ranks.
        // Mirrors: permission?.canSignupGuildRuns ?? true.
        if (guild.RankPermissions is not null)
        {
            var perm = guild.RankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
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

        // Mirrors: getEffectiveGuildPermissions — returns false when roster is absent or stale.
        if (guild?.BlizzardRosterRaw?.Members is null) return false;

        // Roster freshness check: mirrors isGuildRosterFresh (TTL = 1 hour).
        if (guild.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        if (DateTimeOffset.UtcNow - fetchedAt >= TimeSpan.FromHours(1)) return false;

        // Find the best (lowest) matched rank for the raider's characters.
        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;

        int? bestRank = null;
        if (raider.Characters is not null)
        {
            foreach (var character in raider.Characters)
            {
                var key = $"{character.Realm}:{character.Name}";
                if (rankByKey.TryGetValue(key, out var rank))
                {
                    if (bestRank is null || rank < bestRank)
                        bestRank = rank;
                }
            }
        }

        if (bestRank is null) return false;

        // Look up rank permission entry; default canDeleteGuildRuns to (rank 0 only).
        // Mirrors: permission?.canDeleteGuildRuns ?? (rank === 0).
        if (guild.RankPermissions is not null)
        {
            var perm = guild.RankPermissions.FirstOrDefault(rp => rp.Rank == bestRank.Value);
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

        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;

        int? bestRank = null;
        if (raider.Characters is not null)
        {
            foreach (var character in raider.Characters)
            {
                var key = $"{character.Realm}:{character.Name}";
                if (rankByKey.TryGetValue(key, out var rank))
                {
                    if (bestRank is null || rank < bestRank) bestRank = rank;
                }
            }
        }

        var isAdmin = bestRank == 0;

        // Roster-freshness check matches CanCreate/Signup/DeleteGuildRunsAsync —
        // 1-hour TTL after BlizzardRosterFetchedAt.
        var rosterFresh =
            guild.BlizzardRosterFetchedAt is not null
            && DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)
            && DateTimeOffset.UtcNow - fetchedAt < TimeSpan.FromHours(1);

        if (!rosterFresh || bestRank is null)
            return new GuildEffectivePermissions(isAdmin, false, false, false);

        var perm = guild.RankPermissions?.FirstOrDefault(rp => rp.Rank == bestRank.Value);
        var canCreate = perm is not null ? perm.CanCreateGuildRuns : bestRank.Value == 0;
        var canSignup = perm is not null ? perm.CanSignupGuildRuns : true;
        var canDelete = perm is not null ? perm.CanDeleteGuildRuns : bestRank.Value == 0;

        return new GuildEffectivePermissions(isAdmin, canCreate, canSignup, canDelete);
    }
}
