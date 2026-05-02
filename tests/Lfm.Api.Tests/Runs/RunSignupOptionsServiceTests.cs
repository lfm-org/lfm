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

    private static GuildDocument MakeGuild(string memberName = "Guildmain") =>
        new(
            Id: "123",
            GuildId: 123,
            RealmSlug: "silvermoon",
            BlizzardRosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
            BlizzardRosterRaw: new StoredGuildRoster([
                new StoredGuildRosterMember(
                    new StoredGuildRosterMemberCharacter(
                        Name: memberName,
                        Realm: new StoredGuildRosterRealm("silvermoon")),
                    Rank: 4)
            ]));

    private static (
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildRepository> guildRepo,
        Mock<IGuildPermissions> guildPermissions,
        RunSignupOptionsService sut) MakeSut(bool siteAdmin = false)
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var guildRepo = new Mock<IGuildRepository>();
        var guildPermissions = new Mock<IGuildPermissions>();
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var siteAdminService = new Mock<ISiteAdminService>();
        siteAdminService
            .Setup(s => s.IsAdminAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(siteAdmin);

        var sut = new RunSignupOptionsService(
            runsRepo.Object,
            raidersRepo.Object,
            guildRepo.Object,
            guildPermissions.Object,
            siteAdminService.Object,
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
    public async Task GetAsync_site_admin_bypasses_rank_signup_permission()
    {
        var (runsRepo, raidersRepo, guildRepo, guildPermissions, sut) = MakeSut(siteAdmin: true);
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-site-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaider(
                battleNetId: "bnet-site-admin",
                accountProfile: AccountProfile(),
                refreshedAt: DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o")));
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        guildRepo.Setup(r => r.GetAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuild());

        var result = await sut.GetAsync("run-1", MakePrincipal("bnet-site-admin"), CancellationToken.None);

        var ok = Assert.IsType<RunSignupOptionsResult.Ok>(result);
        Assert.Equal(2, ok.Options.Characters.Count);
        Assert.Contains(ok.Options.Characters, c => c.Name == "Guildmain");
        Assert.Contains(ok.Options.Characters, c => c.Name == "Unguildedalt");
    }

    [Fact]
    public async Task GetAsync_site_admin_can_load_options_when_selected_character_is_not_run_guild()
    {
        var (runsRepo, raidersRepo, guildRepo, guildPermissions, sut) = MakeSut(siteAdmin: true);
        var raider = MakeRaider(
            battleNetId: "bnet-site-admin",
            accountProfile: AccountProfile(),
            refreshedAt: DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o")) with
        {
            SelectedCharacterId = "char-alt",
        };
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-site-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        guildRepo.Setup(r => r.GetAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuild());

        var result = await sut.GetAsync("run-1", MakePrincipal("bnet-site-admin"), CancellationToken.None);

        var ok = Assert.IsType<RunSignupOptionsResult.Ok>(result);
        Assert.Equal(2, ok.Options.Characters.Count);
        Assert.Contains(ok.Options.Characters, c => c.Name == "Guildmain");
        Assert.Contains(ok.Options.Characters, c => c.Name == "Unguildedalt");
    }

    [Fact]
    public async Task GetAsync_site_admin_gets_account_characters_when_run_guild_roster_has_no_matches()
    {
        var (runsRepo, raidersRepo, guildRepo, guildPermissions, sut) = MakeSut(siteAdmin: true);
        var raider = MakeRaider(
            battleNetId: "bnet-site-admin",
            accountProfile: AccountProfile(),
            refreshedAt: DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o")) with
        {
            SelectedCharacterId = "char-alt",
        };
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-site-admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildPermissions
            .Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        guildRepo.Setup(r => r.GetAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuild(memberName: "Othermember"));

        var result = await sut.GetAsync("run-1", MakePrincipal("bnet-site-admin"), CancellationToken.None);

        var ok = Assert.IsType<RunSignupOptionsResult.Ok>(result);
        Assert.Equal(2, ok.Options.Characters.Count);
        Assert.Contains(ok.Options.Characters, c => c.Name == "Guildmain");
        Assert.Contains(ok.Options.Characters, c => c.Name == "Unguildedalt");
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

    [Fact]
    public async Task GetAsync_large_roster_uses_single_lookup_per_dependency()
    {
        var accountCharacters = Enumerable.Range(1, 75)
            .Select(i => new StoredBlizzardAccountCharacter(
                Name: $"Guildmain{i}",
                Level: 80,
                Realm: new StoredBlizzardRealmRef("silvermoon", "Silvermoon"),
                PlayableClass: new StoredBlizzardNamedRef(5, "Priest")))
            .ToArray();
        var selectedCharacters = accountCharacters
            .Select((character, index) => new StoredSelectedCharacter(
                Id: $"char-{index + 1}",
                Region: "eu",
                Realm: character.Realm.Slug,
                Name: character.Name,
                GuildId: 123,
                GuildName: "Test Guild",
                ClassId: 5,
                ClassName: "Priest"))
            .ToArray();
        var rosterMembers = accountCharacters
            .Select(character => new StoredGuildRosterMember(
                new StoredGuildRosterMemberCharacter(
                    Name: character.Name,
                    Realm: new StoredGuildRosterRealm("silvermoon")),
                Rank: 4))
            .ToArray();

        var raider = new RaiderDocument(
            Id: "bnet-user",
            BattleNetId: "bnet-user",
            SelectedCharacterId: "char-1",
            Locale: null,
            Characters: selectedCharacters,
            AccountProfileSummary: new StoredBlizzardAccountProfile([
                new StoredBlizzardWowAccount(1, accountCharacters)
            ]),
            AccountProfileRefreshedAt: DateTimeOffset.UtcNow.AddMinutes(-2).ToString("o"));
        var guild = new GuildDocument(
            Id: "123",
            GuildId: 123,
            RealmSlug: "silvermoon",
            BlizzardRosterFetchedAt: DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
            BlizzardRosterRaw: new StoredGuildRoster(rosterMembers));

        var (runsRepo, raidersRepo, guildRepo, guildPermissions, sut) = MakeSut();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRun());
        guildRepo.Setup(r => r.GetAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        var result = await sut.GetAsync("run-1", MakePrincipal(), CancellationToken.None);

        var ok = Assert.IsType<RunSignupOptionsResult.Ok>(result);
        Assert.Equal(75, ok.Options.Characters.Count);
        raidersRepo.Verify(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()), Times.Once);
        runsRepo.Verify(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        guildPermissions.Verify(p => p.CanSignupGuildRunsAsync(raider, It.IsAny<CancellationToken>()), Times.Once);
        guildRepo.Verify(r => r.GetAsync("123", It.IsAny<CancellationToken>()), Times.Once);
    }
}
