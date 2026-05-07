// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Api.Services.Blizzard.Models;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class GuildDocumentRefreshServiceTests
{
    private const string AccessToken = "access-token";

    [Fact]
    public async Task RefreshForCurrentRaiderAsync_Refreshes_Stale_Doc_And_Preserves_Settings()
    {
        var stale = MakeGuildDoc() with
        {
            BlizzardRosterFetchedAt = DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
            Slogan = "Old slogan",
            RankPermissions = [new GuildRankPermission(0, true, true, true)],
            Setup = new GuildSetup("2026-01-01T00:00:00.0000000Z", "Europe/Helsinki", "fi"),
            LastOverrideBy = "admin-1",
            LastOverrideAt = "2026-01-01T01:00:00.0000000Z",
            ETag = "\"old-etag\"",
        };

        var guildRepo = new Mock<IGuildRepository>();
        guildRepo
            .Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDocument doc, CancellationToken _) => doc with { ETag = "\"fresh-etag\"" });
        var blizzard = BlizzardReturning();
        var service = new GuildDocumentRefreshService(guildRepo.Object, blizzard.Object);

        var result = await service.RefreshForCurrentRaiderAsync(
            MakeRaiderDoc(),
            AccessToken,
            stale,
            CancellationToken.None);

        Assert.NotNull(result.Guild);
        Assert.True(result.RefreshAttempted);
        Assert.False(result.UsedCachedFallback);
        Assert.Null(result.Failure);
        Assert.Equal("\"fresh-etag\"", result.Guild!.ETag);
        Assert.Equal("Old slogan", result.Guild.Slogan);
        Assert.Equal(stale.RankPermissions, result.Guild.RankPermissions);
        Assert.Equal(stale.Setup, result.Guild.Setup);
        Assert.Equal("admin-1", result.Guild.LastOverrideBy);
        Assert.Equal("2026-01-01T01:00:00.0000000Z", result.Guild.LastOverrideAt);
        Assert.True(GuildRosterMatcher.IsFresh(result.Guild.BlizzardRosterFetchedAt));

        blizzard.Verify(b => b.GetGuildProfileAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()), Times.Once);
        blizzard.Verify(b => b.GetGuildRosterAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()), Times.Once);
        guildRepo.Verify(r => r.UpsertAsync(
            It.Is<GuildDocument>(d =>
                d.Id == "12345" &&
                d.GuildId == 12345 &&
                d.RealmSlug == "silvermoon" &&
                d.BlizzardProfileRaw!.Name == "Raiders United" &&
                d.BlizzardRosterRaw!.Members!.Single().Rank == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshForCurrentRaiderAsync_Skips_Blizzard_When_Cached_Doc_Is_Fresh()
    {
        var fresh = MakeGuildDoc() with
        {
            BlizzardRosterFetchedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
            BlizzardProfileFetchedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("O"),
            BlizzardRosterRaw = new StoredGuildRoster([]),
            BlizzardProfileRaw = new StoredGuildProfile(
                "Raiders United",
                new StoredGuildProfileRealm("silvermoon", "Silvermoon")),
        };
        var guildRepo = new Mock<IGuildRepository>(MockBehavior.Strict);
        var blizzard = new Mock<IBlizzardProfileClient>(MockBehavior.Strict);
        var service = new GuildDocumentRefreshService(guildRepo.Object, blizzard.Object);

        var result = await service.RefreshForCurrentRaiderAsync(
            MakeRaiderDoc(),
            AccessToken,
            fresh,
            CancellationToken.None);

        Assert.Same(fresh, result.Guild);
        Assert.False(result.RefreshAttempted);
        Assert.False(result.UsedCachedFallback);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task RefreshForCurrentRaiderAsync_Returns_Cached_Fallback_When_Token_Is_Missing()
    {
        var stale = MakeGuildDoc() with
        {
            BlizzardRosterFetchedAt = DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
            BlizzardProfileRaw = new StoredGuildProfile(
                "Raiders United",
                new StoredGuildProfileRealm("silvermoon", "Silvermoon")),
        };
        var guildRepo = new Mock<IGuildRepository>(MockBehavior.Strict);
        var blizzard = new Mock<IBlizzardProfileClient>(MockBehavior.Strict);
        var service = new GuildDocumentRefreshService(guildRepo.Object, blizzard.Object);

        var result = await service.RefreshForCurrentRaiderAsync(
            MakeRaiderDoc(),
            accessToken: null,
            cached: stale,
            CancellationToken.None);

        Assert.Same(stale, result.Guild);
        Assert.False(result.RefreshAttempted);
        Assert.True(result.UsedCachedFallback);
        Assert.Equal(GuildRefreshFailure.MissingAccessToken, result.Failure);
    }

    [Fact]
    public async Task BootstrapForAdminAsync_Refreshes_From_Raider_Character_Context()
    {
        var guildRepo = new Mock<IGuildRepository>();
        guildRepo
            .Setup(r => r.UpsertAsync(It.IsAny<GuildDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDocument doc, CancellationToken _) => doc with { ETag = "\"boot-etag\"" });
        var blizzard = BlizzardReturning();
        var service = new GuildDocumentRefreshService(guildRepo.Object, blizzard.Object);

        var result = await service.BootstrapForAdminAsync(
            "12345",
            AccessToken,
            [MakeRaiderDoc()],
            CancellationToken.None);

        Assert.NotNull(result.Guild);
        Assert.True(result.RefreshAttempted);
        Assert.False(result.UsedCachedFallback);
        Assert.Null(result.Failure);
        Assert.Equal("\"boot-etag\"", result.Guild!.ETag);
        Assert.True(GuildRosterMatcher.IsFresh(result.Guild.BlizzardRosterFetchedAt));
        guildRepo.Verify(r => r.UpsertAsync(
            It.Is<GuildDocument>(d => d.Id == "12345" && d.BlizzardRosterRaw!.Members!.Single().Rank == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefreshExistingAsync_Returns_Cached_Fallback_When_Blizzard_Fails()
    {
        var stale = MakeGuildDoc() with
        {
            BlizzardProfileRaw = new StoredGuildProfile(
                "Raiders United",
                new StoredGuildProfileRealm("silvermoon", "Silvermoon")),
        };
        var guildRepo = new Mock<IGuildRepository>(MockBehavior.Strict);
        var blizzard = new Mock<IBlizzardProfileClient>();
        blizzard
            .Setup(b => b.GetGuildProfileAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upstream down", null, HttpStatusCode.BadGateway));
        blizzard
            .Setup(b => b.GetGuildRosterAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("upstream down", null, HttpStatusCode.BadGateway));
        var service = new GuildDocumentRefreshService(guildRepo.Object, blizzard.Object);

        var result = await service.RefreshExistingAsync(stale, AccessToken, CancellationToken.None);

        Assert.Same(stale, result.Guild);
        Assert.True(result.RefreshAttempted);
        Assert.True(result.UsedCachedFallback);
        Assert.Equal(GuildRefreshFailure.BlizzardUnavailable, result.Failure);
    }

    private static RaiderDocument MakeRaiderDoc() =>
        new(
            Id: "bnet-1",
            BattleNetId: "bnet-1",
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters:
            [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Tankadin",
                    GuildId: 12345,
                    GuildName: "Raiders United"),
            ]);

    private static GuildDocument MakeGuildDoc() =>
        new(
            Id: "12345",
            GuildId: 12345,
            RealmSlug: "silvermoon",
            BlizzardRosterFetchedAt: DateTimeOffset.UtcNow.AddHours(-2).ToString("O"),
            BlizzardProfileFetchedAt: DateTimeOffset.UtcNow.AddHours(-2).ToString("O"));

    private static Mock<IBlizzardProfileClient> BlizzardReturning()
    {
        var blizzard = new Mock<IBlizzardProfileClient>();
        blizzard
            .Setup(b => b.GetGuildProfileAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildProfileResponse(
                "Raiders United",
                new GuildProfileRealmResponse("silvermoon", "Silvermoon"),
                new GuildProfileFactionResponse("Alliance"),
                MemberCount: 42,
                AchievementPoints: 12345));
        blizzard
            .Setup(b => b.GetGuildRosterAsync("silvermoon", "raiders-united", AccessToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRosterResponse(
            [
                new GuildRosterMemberResponse(
                    new GuildRosterMemberCharacterResponse(
                        "Tankadin",
                        new GuildRosterRealmResponse("silvermoon"),
                        Id: 987),
                    Rank: 0),
            ]));
        return blizzard;
    }
}
