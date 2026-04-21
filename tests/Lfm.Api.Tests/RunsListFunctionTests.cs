// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsListFunctionTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1", string? guildId = "12345") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: guildId,
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-1",
        int? guildId = 12345) =>
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
                    GuildName: guildId is not null ? "Test Guild" : null)
            ]);

    private static RunDocument MakeRunDoc(
        string id = "run-1",
        string visibility = "PUBLIC",
        string? creatorBattleNetId = null,
        int? creatorGuildId = null,
        List<RunCharacterEntry>? runCharacters = null) =>
        new RunDocument(
            Id: id,
            StartTime: "2025-06-01T19:00:00Z",
            SignupCloseTime: "2025-06-01T18:30:00Z",
            Description: "Test run",
            ModeKey: "normal",
            Visibility: visibility,
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 1234,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: creatorBattleNetId,
            CreatedAt: "2025-05-01T00:00:00Z",
            Ttl: -1,
            RunCharacters: runCharacters ?? []);

    private static RunCharacterEntry MakeCharacterEntry(string raiderBattleNetId = "bnet-1") =>
        new RunCharacterEntry(
            Id: "char-1",
            CharacterId: "char-1",
            CharacterName: "Testadin",
            CharacterRealm: "Sylvanas",
            CharacterLevel: 80,
            CharacterClassId: 2,
            CharacterClassName: "Paladin",
            CharacterRaceId: 1,
            CharacterRaceName: "Human",
            RaiderBattleNetId: raiderBattleNetId,
            DesiredAttendance: "IN",
            ReviewedAttendance: "IN",
            SpecId: 65,
            SpecName: "Holy",
            Role: "HEALER");

    // ------------------------------------------------------------------
    // Test 1: Happy path — returns sanitized runs for the user's guild
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_sanitized_runs_for_authenticated_user()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");
        var ownCharacter = MakeCharacterEntry(raiderBattleNetId: "bnet-1");
        var otherCharacter = MakeCharacterEntry(raiderBattleNetId: "bnet-other");

        var doc = MakeRunDoc(
            visibility: "GUILD",
            creatorGuildId: 12345,
            runCharacters: [ownCharacter, otherCharacter]);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument> { doc }, null));

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunsListResponse>(ok.Value);
        Assert.Single(response.Items);

        var run = response.Items[0];
        Assert.Equal(doc.Id, run.Id);
        Assert.Equal(2, run.RunCharacters.Count);

        // Own character: IsCurrentUser = true, raiderBattleNetId stripped
        var ownDto = run.RunCharacters.First(c => c.CharacterName == "Testadin" && c.IsCurrentUser);
        Assert.True(ownDto.IsCurrentUser);

        // Other character: IsCurrentUser = false
        var otherDto = run.RunCharacters.First(c => !c.IsCurrentUser);
        Assert.False(otherDto.IsCurrentUser);
    }

    // ------------------------------------------------------------------
    // Test 2: Raider not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_raider_not_found()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1");

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var repo = new Mock<IRunsRepository>();

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
        var errorProp = notFound.Value!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal("Raider not found", errorProp!.GetValue(notFound.Value));
    }

    // ------------------------------------------------------------------
    // Test 3: Empty list — returns 200 with empty array
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_empty_list_when_no_runs_found()
    {
        var principal = MakePrincipal();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument>(), null));

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunsListResponse>(ok.Value);
        Assert.Empty(response.Items);
        Assert.Null(response.ContinuationToken);
    }

    // ------------------------------------------------------------------
    // Test 4: Raider with no guild — falls back to ListForUserAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_uses_ListForUserAsync_when_selected_character_has_no_guild()
    {
        // Raider whose selected character has no guild — GuildResolver returns (null, null)
        // and the function must fall through to ListForUserAsync (non-guild query path).
        var principal = MakePrincipal(battleNetId: "bnet-loner");
        var ownCharacter = MakeCharacterEntry(raiderBattleNetId: "bnet-loner");
        var doc = MakeRunDoc(runCharacters: [ownCharacter]);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForUserAsync("bnet-loner", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument> { doc }, null));

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-loner", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-loner", guildId: null));

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunsListResponse>(ok.Value);
        Assert.Single(response.Items);

        // The guild query must not be called when the raider has no guild.
        repo.Verify(r => r.ListForGuildAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a raider with no guild must use ListForUserAsync, not ListForGuildAsync");
    }

    // ------------------------------------------------------------------
    // Pagination — request parsing and response envelope (W5)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_passes_default_top_200_when_top_query_param_missing()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument>(), null));
        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        await fn.Run(new DefaultHttpContext().Request, MakeFunctionContext(principal), CancellationToken.None);

        repo.Verify(r => r.ListForGuildAsync("12345", "bnet-1", 200, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_clamps_over_max_top_to_200()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument>(), null));
        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?top=5000");

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        await fn.Run(ctx.Request, MakeFunctionContext(principal), CancellationToken.None);

        // Client-requested 5000 is clamped down to the service cap.
        repo.Verify(r => r.ListForGuildAsync("12345", "bnet-1", 200, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_honours_top_query_param_within_range()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument>(), null));
        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?top=25");

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        await fn.Run(ctx.Request, MakeFunctionContext(principal), CancellationToken.None);

        repo.Verify(r => r.ListForGuildAsync("12345", "bnet-1", 25, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_passes_continuation_token_from_query_to_repo()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument>(), null));
        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?continuationToken=abc123");

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        await fn.Run(ctx.Request, MakeFunctionContext(principal), CancellationToken.None);

        repo.Verify(r => r.ListForGuildAsync("12345", "bnet-1", 200, "abc123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_returns_repo_continuation_token_in_response_envelope()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");
        var doc = MakeRunDoc();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.ListForGuildAsync("12345", "bnet-1", It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsPage(new List<RunDocument> { doc }, "next-page-token"));
        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var fn = new RunsListFunction(repo.Object, raidersRepo.Object);
        var result = await fn.Run(new DefaultHttpContext().Request, MakeFunctionContext(principal), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<RunsListResponse>(ok.Value);
        Assert.Single(response.Items);
        Assert.Equal("next-page-token", response.ContinuationToken);
    }
}
