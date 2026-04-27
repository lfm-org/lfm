// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class RunAccessPolicyTests
{
    [Fact]
    public void CanView_allows_public_runs_without_guild_membership()
    {
        var run = MakeRun(visibility: "PUBLIC", creatorBattleNetId: "creator", creatorGuildId: null);

        Assert.True(RunAccessPolicy.CanView(run, "viewer", callerGuildId: null));
    }

    [Fact]
    public void CanView_allows_guild_run_creator()
    {
        var run = MakeRun(visibility: "GUILD", creatorBattleNetId: "creator", creatorGuildId: 123);

        Assert.True(RunAccessPolicy.CanView(run, "creator", callerGuildId: null));
    }

    [Fact]
    public void CanView_allows_same_guild_member_for_guild_run()
    {
        var run = MakeRun(visibility: "GUILD", creatorBattleNetId: "creator", creatorGuildId: 123);

        Assert.True(RunAccessPolicy.CanView(run, "member", callerGuildId: "123"));
    }

    [Fact]
    public void CanView_denies_outside_member_for_guild_run()
    {
        var run = MakeRun(visibility: "GUILD", creatorBattleNetId: "creator", creatorGuildId: 123);

        Assert.False(RunAccessPolicy.CanView(run, "outsider", callerGuildId: "999"));
    }

    [Fact]
    public void IsGuildPeer_allows_same_guild_non_creator_only_for_guild_runs()
    {
        var guildRun = MakeRun(visibility: "GUILD", creatorBattleNetId: "creator", creatorGuildId: 123);
        var publicRun = MakeRun(visibility: "PUBLIC", creatorBattleNetId: "creator", creatorGuildId: 123);

        Assert.True(RunAccessPolicy.IsGuildPeer(guildRun, "peer", callerGuildId: "123"));
        Assert.False(RunAccessPolicy.IsGuildPeer(guildRun, "creator", callerGuildId: "123"));
        Assert.False(RunAccessPolicy.IsGuildPeer(publicRun, "peer", callerGuildId: "123"));
    }

    private static RunDocument MakeRun(
        string visibility,
        string? creatorBattleNetId,
        int? creatorGuildId) =>
        new(
            Id: "run-1",
            StartTime: "2026-06-01T19:00:00Z",
            SignupCloseTime: "2026-06-01T18:30:00Z",
            Description: "Test run",
            ModeKey: "NORMAL:20",
            Visibility: visibility,
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 42,
            InstanceName: "Test Instance",
            CreatorBattleNetId: creatorBattleNetId,
            CreatedAt: "2026-05-01T00:00:00Z",
            Ttl: 86400,
            RunCharacters: []);
}
