using FluentAssertions;
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
            CreatedAt: "2026-04-01T10:00:00Z",
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

    private static RunsCancelSignupFunction MakeFunction(
        Mock<IRunsRepository> runsRepo,
        TestLogger<RunsCancelSignupFunction>? logger = null)
    {
        return new RunsCancelSignupFunction(
            runsRepo.Object,
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

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<RunDetailDto>().Subject;
        dto.RunCharacters.Should().HaveCount(0);

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

        result.Should().BeOfType<NotFoundObjectResult>();

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: [RequireAuth] attribute is present on Run method
    // ------------------------------------------------------------------

    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(RunsCancelSignupFunction).GetMethod(nameof(RunsCancelSignupFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "RunsCancelSignupFunction.Run must carry [RequireAuth]");
    }

    // ------------------------------------------------------------------
    // Test 4: Happy path — emits signup.cancel audit event
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
        var fn = MakeFunction(runsRepo, logger);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakeDeleteRequest(), "run-1", ctx, CancellationToken.None);

        logger.Entries.Should().ContainSingle(e => e.IsAudit(
            action: "signup.cancel",
            actorId: "bnet-user",
            result: "success"),
            "success path must emit a signup.cancel audit event with the battleNetId and result");
    }
}
