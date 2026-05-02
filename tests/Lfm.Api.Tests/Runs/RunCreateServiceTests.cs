// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Repositories;
using Lfm.Api.Runs;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Runs;

/// <summary>
/// Unit tests for <see cref="RunCreateService"/>. Each test exercises one
/// branch of the policy that used to live inline in <c>RunsCreateFunction.Run</c>;
/// the function-level tests in <see cref="RunsCreateFunctionTests"/> stay as
/// the HTTP-shaped integration coverage on top of this service.
/// </summary>
public class RunCreateServiceTests
{
    // Anchored to UtcNow so the fixtures never become time bombs against a
    // future-dated assertion.
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");

    private static SessionPrincipal MakePrincipal(
        string battleNetId = "bnet-admin") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Admin#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaiderWithGuild(
        string battleNetId = "bnet-admin",
        int guildId = 12345,
        string guildName = "Test Guild") =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Testchar",
                    GuildId: guildId,
                    GuildName: guildName)
            ]);

    private static RaiderDocument MakeRaiderWithoutGuild(
        string battleNetId = "bnet-admin") =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: null,
            Locale: null,
            Characters: null);

    private static CreateRunRequest MakeRequest(string visibility = "GUILD") =>
        new CreateRunRequest(
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Created run",
            Visibility: visibility,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            Difficulty: "NORMAL",
            Size: 20);

    private static (
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildPermissions> guildPermissions,
        RunCreateService sut) MakeSut()
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        var siteAdmin = new Mock<ISiteAdminService>();
        var sut = new RunCreateService(
            runsRepo.Object,
            raidersRepo.Object,
            guildPermissions.Object,
            siteAdmin.Object);
        return (runsRepo, raidersRepo, guildPermissions, sut);
    }

    [Fact]
    public async Task CreateAsync_SiteAdminWithGuild_BypassesGuildRankCreatePermission()
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        var siteAdmin = new Mock<ISiteAdminService>();
        var sut = new RunCreateService(
            runsRepo.Object,
            raidersRepo.Object,
            guildPermissions.Object,
            siteAdmin.Object);
        var principal = MakePrincipal("bnet-site-admin");
        var raider = MakeRaiderWithGuild("bnet-site-admin", guildId: 123, guildName: "Test Guild");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-site-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        guildPermissions.Setup(p => p.CanCreateGuildRunsAsync(raider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        siteAdmin.Setup(s => s.IsAdminAsync("bnet-site-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        runsRepo.Setup(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, CancellationToken _) => doc);

        var result = await sut.CreateAsync(MakeRequest(), principal, CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        Assert.Equal("bnet-site-admin", ok.Run.CreatorBattleNetId);
        Assert.Equal(123, ok.Run.CreatorGuildId);
        Assert.Equal("GUILD", ok.Run.Visibility);
    }

    [Fact]
    public async Task CreateAsync_RaiderMissing_ReturnsNotFound()
    {
        var (_, raidersRepo, _, sut) = MakeSut();
        var principal = MakePrincipal("bnet-admin");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var result = await sut.CreateAsync(MakeRequest(), principal, CancellationToken.None);

        var notFound = Assert.IsType<RunOperationResult.NotFound>(result);
        Assert.Equal("raider-not-found", notFound.Code);
    }

    [Fact]
    public async Task CreateAsync_WithoutGuild_ReturnsBadRequest()
    {
        var (_, raidersRepo, _, sut) = MakeSut();
        var principal = MakePrincipal("bnet-admin");
        var raider = MakeRaiderWithoutGuild("bnet-admin");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var result = await sut.CreateAsync(MakeRequest(), principal, CancellationToken.None);

        var badRequest = Assert.IsType<RunOperationResult.BadRequest>(result);
        Assert.Equal("guild-required", badRequest.Code);
    }

    [Fact]
    public async Task CreateAsync_WithoutPermission_ReturnsForbidden()
    {
        var (_, raidersRepo, guildPermissions, sut) = MakeSut();
        var principal = MakePrincipal("bnet-member");
        var raider = MakeRaiderWithGuild("bnet-member");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        guildPermissions.Setup(p => p.CanCreateGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.CreateAsync(MakeRequest(), principal, CancellationToken.None);

        var forbidden = Assert.IsType<RunOperationResult.Forbidden>(result);
        Assert.Equal("guild-rank-denied", forbidden.Code);
        Assert.Equal("guild rank denied", forbidden.AuditReason);
    }

    [Fact]
    public async Task CreateAsync_PersistsGuildVisibility_WhenLegacyClientSendsPublic()
    {
        var (runsRepo, raidersRepo, guildPermissions, sut) = MakeSut();
        var principal = MakePrincipal("bnet-admin");
        var raider = MakeRaiderWithGuild("bnet-admin");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        guildPermissions.Setup(p => p.CanCreateGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        runsRepo.Setup(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, CancellationToken _) => doc);

        var result = await sut.CreateAsync(MakeRequest("PUBLIC"), principal, CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        Assert.NotNull(ok.Run.Id);
        Assert.NotEqual(string.Empty, ok.Run.Id);
        Assert.Equal("bnet-admin", ok.Run.CreatorBattleNetId);
        Assert.Equal("GUILD", ok.Run.Visibility);
        Assert.True(ok.Run.Ttl >= 86400);
        Assert.Equal("NORMAL:20", ok.Run.ModeKey);
        Assert.Empty(ok.Run.RunCharacters);
        runsRepo.Verify(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_GuildRunHappyPath_AssignsGuildId()
    {
        var (runsRepo, raidersRepo, guildPermissions, sut) = MakeSut();
        var principal = MakePrincipal("bnet-admin");
        var raider = MakeRaiderWithGuild("bnet-admin", guildId: 123, guildName: "Test Guild");
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        guildPermissions.Setup(p => p.CanCreateGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        runsRepo.Setup(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument doc, CancellationToken _) => doc);

        var result = await sut.CreateAsync(MakeRequest("GUILD"), principal, CancellationToken.None);

        var ok = Assert.IsType<RunOperationResult.Ok>(result);
        Assert.Equal(123, ok.Run.CreatorGuildId);
        Assert.Equal("Test Guild", ok.Run.CreatorGuild);
        Assert.Equal("GUILD", ok.Run.Visibility);
    }
}
