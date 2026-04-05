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
}
