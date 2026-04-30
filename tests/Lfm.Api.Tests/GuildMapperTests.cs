// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests;

public class GuildMapperTests
{
    private static GuildDocument SampleDoc() => new(
        Id: "123",
        GuildId: 123,
        RealmSlug: "ravencrest",
        Slogan: "for the alliance",
        BlizzardRosterFetchedAt: DateTimeOffset.UtcNow.ToString("o"),
        BlizzardProfileRaw: new BlizzardGuildProfileRaw(
            Name: "Test Guild",
            Realm: new BlizzardGuildProfileRealm(Slug: "ravencrest", Name: "Ravencrest"),
            Faction: new BlizzardGuildProfileFaction(Name: "Alliance"),
            MemberCount: 50),
        RankPermissions: new[]
        {
            new GuildRankPermission(Rank: 0, CanCreateGuildRuns: true, CanSignupGuildRuns: true, CanDeleteGuildRuns: true),
            new GuildRankPermission(Rank: 5, CanCreateGuildRuns: false, CanSignupGuildRuns: true, CanDeleteGuildRuns: false),
        });

    [Fact]
    public void MapToDto_RankZero_PopulatesPermissionsAndSettings()
    {
        var perms = new GuildEffectivePermissions(
            IsAdmin: true,
            CanCreateGuildRuns: true,
            CanSignupGuildRuns: true,
            CanDeleteGuildRuns: true);

        var dto = GuildMapper.MapToDto(SampleDoc(), perms);

        Assert.NotNull(dto.Guild);
        Assert.True(dto.Editor.CanEdit);
        Assert.True(dto.MemberPermissions.CanCreateGuildRuns);
        Assert.True(dto.MemberPermissions.CanSignupGuildRuns);
        Assert.True(dto.MemberPermissions.CanDeleteGuildRuns);
        Assert.NotNull(dto.Settings);
        Assert.Equal(2, dto.Settings.RankPermissions.Count);
    }

    [Fact]
    public void MapToDto_NonAdmin_OmitsSettings_PopulatesMemberPermissions()
    {
        var perms = new GuildEffectivePermissions(
            IsAdmin: false,
            CanCreateGuildRuns: false,
            CanSignupGuildRuns: true,
            CanDeleteGuildRuns: false);

        var dto = GuildMapper.MapToDto(SampleDoc(), perms);

        Assert.False(dto.Editor.CanEdit);
        Assert.Null(dto.Settings);
        Assert.False(dto.MemberPermissions.CanCreateGuildRuns);
        Assert.True(dto.MemberPermissions.CanSignupGuildRuns);
        Assert.False(dto.MemberPermissions.CanDeleteGuildRuns);
    }

    [Fact]
    public void NoGuildDto_AllFalse()
    {
        var dto = GuildMapper.NoGuildDto();
        Assert.Null(dto.Guild);
        Assert.False(dto.Editor.CanEdit);
        Assert.False(dto.MemberPermissions.CanCreateGuildRuns);
        Assert.False(dto.MemberPermissions.CanSignupGuildRuns);
        Assert.False(dto.MemberPermissions.CanDeleteGuildRuns);
        Assert.Null(dto.Settings);
    }
}
