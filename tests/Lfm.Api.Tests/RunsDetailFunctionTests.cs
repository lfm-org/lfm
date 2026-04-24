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

public class RunsDetailFunctionTests
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

    private static SessionPrincipal MakePrincipal(
        string battleNetId = "bnet-1",
        string? guildId = "12345") =>
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
        List<RunCharacterEntry>? runCharacters = null,
        string? etag = null) =>
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
            RunCharacters: runCharacters ?? [],
            ETag: etag);

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
    // Test 1: Happy path — returns sanitized run detail for authenticated user
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_sanitized_run_detail_for_authenticated_user()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");
        var ownCharacter = MakeCharacterEntry(raiderBattleNetId: "bnet-1");
        var otherCharacter = MakeCharacterEntry(raiderBattleNetId: "bnet-other");

        var doc = MakeRunDoc(
            id: "run-1",
            visibility: "GUILD",
            creatorGuildId: 12345,
            runCharacters: [ownCharacter, otherCharacter]);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-1", guildId: 12345));

        var fn = new RunsDetailFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-1",
            ctx,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RunDetailDto>(ok.Value);
        Assert.Equal("run-1", dto.Id);
        Assert.Equal(2, dto.RunCharacters.Count);

        // Own character: IsCurrentUser = true, raiderBattleNetId stripped from DTO
        var ownDto = dto.RunCharacters.First(c => c.IsCurrentUser);
        Assert.True(ownDto.IsCurrentUser);

        // Other character: IsCurrentUser = false
        var otherDto = dto.RunCharacters.First(c => !c.IsCurrentUser);
        Assert.False(otherDto.IsCurrentUser);
    }

    // ------------------------------------------------------------------
    // Test 2: Run not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_run_does_not_exist()
    {
        var principal = MakePrincipal();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("missing-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var raidersRepo = new Mock<IRaidersRepository>();

        var fn = new RunsDetailFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "missing-id",
            ctx,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.IsType<ProblemDetails>(problem.Value);

        // Ordering contract: the run must be looked up before the raider.
        // If a future refactor reverses the order, this short-circuit test
        // would silently still pass without the Verify below.
        raidersRepo.Verify(
            r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a missing run must short-circuit before any raider lookup");
    }

    // ------------------------------------------------------------------
    // Test 3: GUILD run — returns 404 when raider document is missing
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_for_guild_run_when_raider_not_found()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1");

        var doc = MakeRunDoc(
            id: "run-guild",
            visibility: "GUILD",
            creatorBattleNetId: "bnet-other",
            creatorGuildId: 12345);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-guild", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var fn = new RunsDetailFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-guild",
            ctx,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.IsType<ProblemDetails>(problem.Value);
    }

    // ------------------------------------------------------------------
    // Test 4: GUILD run — returns 404 when user is not creator or guild member
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_for_guild_run_when_user_is_not_member_or_creator()
    {
        // User belongs to guild 99999, but run was created by guild 12345.
        var principal = MakePrincipal(battleNetId: "bnet-outsider", guildId: "99999");

        var doc = MakeRunDoc(
            id: "run-guild",
            visibility: "GUILD",
            creatorBattleNetId: "bnet-creator",
            creatorGuildId: 12345);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-guild", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-outsider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-outsider", guildId: 99999));

        var fn = new RunsDetailFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(
            new DefaultHttpContext().Request,
            "run-guild",
            ctx,
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problem.StatusCode);
        Assert.IsType<ProblemDetails>(problem.Value);
    }

    // ------------------------------------------------------------------
    // Test 5: Response carries ETag header mirroring the stored _etag
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_etag_header_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-1", guildId: "12345");
        var doc = MakeRunDoc(
            id: "run-1",
            visibility: "PUBLIC",
            etag: "\"cosmos-etag-xyz\"");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var raidersRepo = new Mock<IRaidersRepository>();
        var fn = new RunsDetailFunction(repo.Object, raidersRepo.Object);
        var ctx = MakeFunctionContext(principal);

        var request = new DefaultHttpContext().Request;
        var result = await fn.Run(request, "run-1", ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("\"cosmos-etag-xyz\"", request.HttpContext.Response.Headers.ETag.ToString());
    }
}
