using Lfm.Api.Auth;
using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

/// <summary>
/// Checks whether the principal holds rank 0 (guild master) in their guild's
/// Blizzard roster, mirroring the TypeScript <c>resolveGuildEditor</c> logic in
/// <c>functions/src/lib/guild/context.ts</c>.
/// </summary>
public sealed class GuildPermissions(IGuildRepository guildRepo, IRaidersRepository raidersRepo) : IGuildPermissions
{
    public async Task<bool> IsAdminAsync(SessionPrincipal principal, CancellationToken ct)
    {
        if (principal.GuildId is null) return false;

        var guildTask = guildRepo.GetAsync(principal.GuildId, ct);
        var raiderTask = raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);

        await Task.WhenAll(guildTask, raiderTask);

        var guild = guildTask.Result;
        var raider = raiderTask.Result;

        if (guild?.BlizzardRosterRaw?.Members is null || raider is null) return false;

        // Build lookup maps mirroring resolveMatchedGuildRanks in
        // functions/src/lib/guild-member-match.ts.
        var rankById = new Dictionary<int, int>();
        var rankByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var member in guild.BlizzardRosterRaw.Members)
        {
            if (member.Character.Id.HasValue)
                rankById[member.Character.Id.Value] = member.Rank;
            rankByKey[$"{member.Character.Realm.Slug}:{member.Character.Name}"] = member.Rank;
        }

        // Walk stored characters — return true as soon as we find a rank-0 match.
        if (raider.Characters is null) return false;
        foreach (var character in raider.Characters)
        {
            // Match by Blizzard character ID first (more reliable).
            // StoredSelectedCharacter.SpecializationsSummary is the closest proxy
            // for "profile summary id" in the .NET model; we use the raider's stored
            // character name+realm as the fallback key.
            var key = $"{character.Realm}:{character.Name}";
            if (rankByKey.TryGetValue(key, out var rankByName) && rankByName == 0) return true;
        }

        return false;
    }

    public async Task<bool> CanCreateGuildRunsAsync(SessionPrincipal principal, CancellationToken ct)
    {
        if (principal.GuildId is null) return false;

        var guildTask = guildRepo.GetAsync(principal.GuildId, ct);
        var raiderTask = raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);

        await Task.WhenAll(guildTask, raiderTask);

        var guild = guildTask.Result;
        var raider = raiderTask.Result;

        // Mirrors: getEffectiveGuildPermissions — returns false when roster is absent or stale.
        if (guild?.BlizzardRosterRaw?.Members is null || raider is null) return false;

        // Roster freshness check: mirrors isGuildRosterFresh (TTL = 1 hour).
        if (guild.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        if (DateTimeOffset.UtcNow - fetchedAt >= TimeSpan.FromHours(1)) return false;

        // Find the best (lowest) matched rank for the principal's characters.
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

    public async Task<bool> CanDeleteGuildRunsAsync(SessionPrincipal principal, CancellationToken ct)
    {
        if (principal.GuildId is null) return false;

        var guildTask = guildRepo.GetAsync(principal.GuildId, ct);
        var raiderTask = raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);

        await Task.WhenAll(guildTask, raiderTask);

        var guild = guildTask.Result;
        var raider = raiderTask.Result;

        // Mirrors: getEffectiveGuildPermissions — returns false when roster is absent or stale.
        if (guild?.BlizzardRosterRaw?.Members is null || raider is null) return false;

        // Roster freshness check: mirrors isGuildRosterFresh (TTL = 1 hour).
        if (guild.BlizzardRosterFetchedAt is null) return false;
        if (!DateTimeOffset.TryParse(guild.BlizzardRosterFetchedAt, out var fetchedAt)) return false;
        if (DateTimeOffset.UtcNow - fetchedAt >= TimeSpan.FromHours(1)) return false;

        // Find the best (lowest) matched rank for the principal's characters.
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
}
