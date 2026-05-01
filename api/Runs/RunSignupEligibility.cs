// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;

namespace Lfm.Api.Runs;

public sealed class RunSignupEligibility(IGuildRepository guildRepo) : IRunSignupEligibility
{
    public async Task<bool> IsSignupCharacterInRunGuildAsync(
        RunDocument run,
        StoredSelectedCharacter signupCharacter,
        CancellationToken ct)
    {
        if (run.CreatorGuildId is null)
            return false;

        var guild = await guildRepo.GetAsync(run.CreatorGuildId.Value.ToString(), ct);
        if (guild?.BlizzardRosterRaw is null)
            return false;

        if (!GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt))
            return false;

        return GuildRosterMatcher.Match(guild.BlizzardRosterRaw, signupCharacter) is not null;
    }
}
