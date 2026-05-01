// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Api.Options;
using Lfm.Api.Repositories;
using Lfm.Api.Runs;
using Lfm.Api.Services;
using Lfm.Contracts.Characters;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Runs;

public class RunSignupOptionsServiceTests
{
    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-user") =>
        new(
            BattleNetId: battleNetId,
            BattleTag: "User#1234",
            GuildId: null,
            GuildName: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaider(
        string battleNetId = "bnet-user",
        int? guildId = 123,
        StoredBlizzardAccountProfile? accountProfile = null,
        string? refreshedAt = null) =>
        new(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: "char-1",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Guildmain",
                    GuildId: guildId,
                    GuildName: guildId is null ? null : "Test Guild",
                    ClassId: 5,
                    ClassName: "Priest"),
                new StoredSelectedCharacter(
                    Id: "char-alt",
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Unguildedalt",
                    ClassId: 1,
                    ClassName: "Warrior")
            ],
            AccountProfileSummary: accountProfile,
            AccountProfileRefreshedAt: refreshedAt);

    private static StoredBlizzardAccountProfile AccountProfile() =>
        new([
            new StoredBlizzardWowAccount(
                Id: 1,
                Characters: [
                    new StoredBlizzardAccountCharacter(
                        Name: "Guildmain",
                        Level: 80,
                        Realm: new StoredBlizzardRealmRef("silvermoon", "Silvermoon"),
                        PlayableClass: new StoredBlizzardNamedRef(5, "Priest")),
                    new StoredBlizzardAccountCharacter(
                        Name: "Unguildedalt",
                        Level: 80,
                        Realm: new StoredBlizzardRealmRef("silvermoon", "Silvermoon"),
                        PlayableClass: new StoredBlizzardNamedRef(1, "Warrior"))
                ])
        ]);

    private static RunDocument MakeRun(int? creatorGuildId = 123) =>
        new(
            Id: "run-1",
            StartTime: DateTimeOffset.UtcNow.AddHours(24).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(22).ToString("o"),
            Description: "Test run",
            ModeKey: "NORMAL:10",
            Visibility: "GUILD",
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-1).ToString("o"),
            Ttl: 86400,
            RunCharacters: [],
            Difficulty: "NORMAL",
            Size: 10);

    private static GuildDocument MakeGuild() =>
        new(
            Id: "123",
            GuildId: 123,
            RealmSlug: "silvermoon",
            BlizzardRosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
            BlizzardRosterRaw: new StoredGuildRoster([
                new StoredGuildRosterMember(
                    new StoredGuildRosterMemberCharacter(
                        Name: "Guildmain",
                        Realm: new StoredGuildRosterRealm("silvermoon")),
                    Rank: 4)
            ]));

    private static (
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildRepository> guildRepo,
        Mock<IGuildPermissions> guildPermissions,
        RunSignupOptionsService sut) MakeSut()
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildRepo = new Mock<IGuildRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new RunSignupOptionsService(
            runsRepo.Object,
            raidersRepo.Object,
            guildRepo.Object,
            guildPermissions.Object,
            Microsoft.Extensions.Options.Options.Create(new BlizzardOptions
            {
                ClientId = "client",
                ClientSecret = "secret",
                Region = "EU",
                RedirectUri = "https://example.com/callback",
                AppBaseUrl = "https://example.com",
            }));

        return (runsRepo, raidersRepo, guildRepo, guildPermissions, sut);
    }

    [Fact]
    public async Task GetAsync_returns_needs_refresh_when_account_cache_is_missing()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider());
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());

        var result = await sut.GetAsync("run-1", MakePrincipal(), CancellationToken.None);

        Assert.IsType<RunSignupOptionsResult.NeedsRefresh>(result);
    }

    [Fact]
    public async Task GetAsync_filters_account_characters_to_run_guild_roster()
    {
        var (runsRepo, raidersRepo, guildRepo, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider(
                accountProfile: AccountProfile(),
                refreshedAt: DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o")));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildRepo.Setup(r => r.GetAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuild());

        var result = await sut.GetAsync("run-1", MakePrincipal(), CancellationToken.None);

        var ok = Assert.IsType<RunSignupOptionsResult.Ok>(result);
        var character = Assert.Single(ok.Options.Characters);
        Assert.Equal("Guildmain", character.Name);
        Assert.Equal("silvermoon", character.Realm);
    }

    [Fact]
    public async Task GetAsync_returns_forbidden_when_caller_rank_cannot_signup()
    {
        var (runsRepo, raidersRepo, _, guildPermissions, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider());
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.GetAsync("run-1", MakePrincipal(), CancellationToken.None);

        var forbidden = Assert.IsType<RunSignupOptionsResult.Forbidden>(result);
        Assert.Equal("guild-rank-denied", forbidden.Code);
    }

    [Fact]
    public async Task GetAsync_returns_not_found_when_run_is_not_visible_to_caller()
    {
        var (runsRepo, raidersRepo, _, _, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider(guildId: 999));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun(creatorGuildId: 123));

        var result = await sut.GetAsync("run-1", MakePrincipal(), CancellationToken.None);

        var notFound = Assert.IsType<RunSignupOptionsResult.NotFound>(result);
        Assert.Equal("run-not-found", notFound.Code);
    }
}
