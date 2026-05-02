// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Mappers;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Runs;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Runs;

public sealed class RunSignupOptionsService(
    IRunsRepository runsRepo,
    IRaidersRepository raidersRepo,
    IGuildRepository guildRepo,
    IGuildPermissions guildPermissions,
    ISiteAdminService siteAdmin,
    IOptions<BlizzardOptions> blizzardOptions) : IRunSignupOptionsService
{
    private readonly BlizzardOptions _blizzardOptions = blizzardOptions.Value;

    public async Task<RunSignupOptionsResult> GetAsync(string runId, SessionPrincipal principal, CancellationToken ct)
    {
        var raider = await raidersRepo.GetByBattleNetIdAsync(principal.BattleNetId, ct);
        if (raider is null)
            return new RunSignupOptionsResult.NotFound("raider-not-found", "Raider not found.");

        var run = await runsRepo.GetByIdAsync(runId, ct);
        if (run is null)
            return new RunSignupOptionsResult.NotFound("run-not-found", "Run not found.");

        bool? callerIsSiteAdmin = null;
        var (callerGuildId, _) = GuildResolver.FromRaider(raider);
        if (!RunAccessPolicy.CanView(run, principal.BattleNetId, callerGuildId))
        {
            callerIsSiteAdmin = await siteAdmin.IsAdminAsync(principal.BattleNetId, ct);
            if (!callerIsSiteAdmin.Value)
                return new RunSignupOptionsResult.NotFound("run-not-found", "Run not found.");
        }

        var canSignup = await guildPermissions.CanSignupGuildRunsAsync(raider, ct);
        if (!canSignup)
        {
            callerIsSiteAdmin ??= await siteAdmin.IsAdminAsync(principal.BattleNetId, ct);
            if (!callerIsSiteAdmin.Value)
                return new RunSignupOptionsResult.Forbidden(
                    "guild-rank-denied",
                    "Guild signup is not enabled for your rank.");
        }

        if (!BattleNetCharactersFunction.ShouldServeCachedAccountProfile(raider))
            return new RunSignupOptionsResult.NeedsRefresh();

        var region = _blizzardOptions.Region.ToLowerInvariant();
        var characters = AccountCharacterMapper.MapToCharacterDtos(
            raider.AccountProfileSummary!,
            region,
            raider.Characters,
            raider.PortraitCache);

        var filtered = callerIsSiteAdmin == true
            ? characters
            : await FilterGuildCharactersAsync(run, characters, ct);
        return new RunSignupOptionsResult.Ok(new RunSignupOptionsDto(filtered));
    }

    private async Task<IReadOnlyList<CharacterDto>> FilterGuildCharactersAsync(
        RunDocument run,
        IReadOnlyList<CharacterDto> characters,
        CancellationToken ct)
    {
        if (run.CreatorGuildId is null)
            return [];

        var guild = await guildRepo.GetAsync(run.CreatorGuildId.Value.ToString(), ct);
        if (guild?.BlizzardRosterRaw is null)
            return [];

        if (!GuildRosterMatcher.IsFresh(guild.BlizzardRosterFetchedAt))
            return [];

        return characters
            .Where(c => GuildRosterMatcher.Match(guild.BlizzardRosterRaw, c) is not null)
            .ToList();
    }
}
