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

public class RunsCancelSignupFunctionTests
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
        string battleNetId = "bnet-user",
        string? guildId = null,
        string? guildName = null) =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "User#1234",
            GuildId: guildId,
            GuildName: guildName,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static HttpRequest MakeDeleteRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "DELETE";
        return httpContext.Request;
    }

    private static RunDocument MakeRunDoc(
        string id = "run-1",
        string visibility = "PUBLIC",
        int? creatorGuildId = null,
        IReadOnlyList<RunCharacterEntry>? runCharacters = null) =>
        new RunDocument(
            Id: id,
            StartTime: DateTimeOffset.UtcNow.AddHours(24).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(22).ToString("o"),
            Description: "Test run",
            ModeKey: "NORMAL:10",
            Visibility: visibility,
            CreatorGuild: "Test Guild",
            CreatorGuildId: creatorGuildId,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: runCharacters ?? []);

    private static RunCharacterEntry MakeCharacterEntry(string battleNetId = "bnet-user") =>
        new RunCharacterEntry(
            Id: "entry-1",
            CharacterId: "char-1",
            CharacterName: "Testchar",
            CharacterRealm: "silvermoon",
            CharacterLevel: 70,
            CharacterClassId: 5,
            CharacterClassName: "Priest",
            CharacterRaceId: 4,
            CharacterRaceName: "Dwarf",
            RaiderBattleNetId: battleNetId,
            DesiredAttendance: "IN",
            ReviewedAttendance: "IN",
            SpecId: 256,
            SpecName: "Shadow",
            Role: null);

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-user",
        int? guildId = null) =>
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

    private static RunsCancelSignupFunction MakeFunction(
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository>? raidersRepo = null,
        TestLogger<RunsCancelSignupFunction>? logger = null)
    {
        return new RunsCancelSignupFunction(
            runsRepo.Object,
            (raidersRepo ?? new Mock<IRaidersRepository>()).Object,
            logger ?? new TestLogger<RunsCancelSignupFunction>());
    }

    // ------------------------------------------------------------------
    // Test 1: Happy path — removes user's signup and returns 200
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_cancels_signup_and_returns_200()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var entry = MakeCharacterEntry(battleNetId: "bnet-user");
        var run = MakeRunDoc(runCharacters: [entry]);

        var updatedRun = run with { RunCharacters = [] };

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRun);

        var fn = MakeFunction(runsRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RunDetailDto>(okResult.Value);
        Assert.Empty(dto.RunCharacters);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: User not signed up — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_user_not_signed_up()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var entry = MakeCharacterEntry(battleNetId: "bnet-other");
        var run = MakeRunDoc(runCharacters: [entry]);

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var fn = MakeFunction(runsRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: GUILD run — returns 404 when raider document is missing
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_for_guild_run_when_raider_not_found()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var entry = MakeCharacterEntry(battleNetId: "bnet-user");
        var run = MakeRunDoc(
            visibility: "GUILD",
            creatorGuildId: 12345,
            runCharacters: [entry]);

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var fn = MakeFunction(runsRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 4: GUILD run happy path — same-guild caller cancels signup (200)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_cancels_signup_for_guild_run_when_caller_in_same_guild()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user", guildId: "12345");
        var entry = MakeCharacterEntry(battleNetId: "bnet-user");
        var run = MakeRunDoc(
            visibility: "GUILD",
            creatorGuildId: 12345,
            runCharacters: [entry]);

        var updatedRun = run with { RunCharacters = [] };

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRun);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-user", guildId: 12345));

        var fn = MakeFunction(runsRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RunDetailDto>(okResult.Value);
        Assert.Empty(dto.RunCharacters);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 5: GUILD run outsider — returns 404 (no information leakage)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_for_guild_run_when_caller_in_different_guild()
    {
        // Caller belongs to guild 99999 — different from the run's creator guild (12345).
        var principal = MakePrincipal(battleNetId: "bnet-outsider", guildId: "99999");
        var run = MakeRunDoc(
            visibility: "GUILD",
            creatorGuildId: 12345,
            runCharacters: []);

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-outsider", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeRaiderDoc("bnet-outsider", guildId: 99999));

        var fn = MakeFunction(runsRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 6: Happy path — emits signup.cancel audit event
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_signup_cancel_audit_event_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var entry = MakeCharacterEntry(battleNetId: "bnet-user");
        var run = MakeRunDoc(runCharacters: [entry]);

        var updatedRun = run with { RunCharacters = [] };

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRun);

        var logger = new TestLogger<RunsCancelSignupFunction>();
        var fn = MakeFunction(runsRepo, logger: logger);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "signup.cancel",
            actorId: "bnet-user",
            result: "success"));
    }
}
