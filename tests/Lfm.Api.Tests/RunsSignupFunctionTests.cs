using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsSignupFunctionTests
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

    private static HttpRequest MakePostRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
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

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-user",
        string characterId = "char-1") =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: characterId,
            Locale: null,
            Characters: [
                new StoredSelectedCharacter(
                    Id: characterId,
                    Region: "eu",
                    Realm: "silvermoon",
                    Name: "Testchar")
            ]);

    // ------------------------------------------------------------------
    // Test 1: Happy path — new signup returns 200 with sanitized run
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_adds_signup_and_returns_200()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var run = MakeRunDoc();
        var raider = MakeRaiderDoc(battleNetId: "bnet-user", characterId: "char-1");

        var requestBody = new
        {
            characterId = "char-1",
            desiredAttendance = "IN",
            specId = (int?)null,
        };

        var updatedRun = run with
        {
            RunCharacters = [
                new RunCharacterEntry(
                    Id: "new-entry-id",
                    CharacterId: "char-1",
                    CharacterName: "Testchar",
                    CharacterRealm: "silvermoon",
                    CharacterLevel: 0,
                    CharacterClassId: 0,
                    CharacterClassName: "",
                    CharacterRaceId: 0,
                    CharacterRaceName: "",
                    RaiderBattleNetId: "bnet-user",
                    DesiredAttendance: "IN",
                    ReviewedAttendance: "IN",
                    SpecId: null,
                    SpecName: null,
                    Role: null)
            ]
        };

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);
        runsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRun);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var permissions = new Mock<IGuildPermissions>();

        var fn = new RunsSignupFunction(runsRepo.Object, raidersRepo.Object, permissions.Object);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<RunDetailDto>().Subject;
        dto.RunCharacters.Should().HaveCount(1);
        dto.RunCharacters[0].IsCurrentUser.Should().BeTrue();

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Run not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_run_does_not_exist()
    {
        var principal = MakePrincipal();

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("missing-run", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var raidersRepo = new Mock<IRaidersRepository>();
        var permissions = new Mock<IGuildPermissions>();

        var fn = new RunsSignupFunction(runsRepo.Object, raidersRepo.Object, permissions.Object);
        var ctx = MakeFunctionContext(principal);

        var requestBody = new { characterId = "char-1", desiredAttendance = "IN" };
        var result = await fn.Run(MakePostRequest(requestBody), "missing-run", ctx, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
        raidersRepo.Verify(r => r.GetByBattleNetIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: GUILD run — caller lacks canSignupGuildRuns — returns 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_for_guild_run_when_caller_lacks_signup_permission()
    {
        // Caller is in same guild but lacks canSignupGuildRuns.
        var principal = MakePrincipal(battleNetId: "bnet-member", guildId: "12345");
        var run = MakeRunDoc(visibility: "GUILD", creatorGuildId: 12345);

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var raidersRepo = new Mock<IRaidersRepository>();
        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanSignupGuildRunsAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = new RunsSignupFunction(runsRepo.Object, raidersRepo.Object, permissions.Object);
        var ctx = MakeFunctionContext(principal);

        var requestBody = new { characterId = "char-1", desiredAttendance = "IN" };
        var result = await fn.Run(MakePostRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 4: [RequireAuth] attribute is present on Run method
    // ------------------------------------------------------------------

    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(RunsSignupFunction).GetMethod(nameof(RunsSignupFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "RunsSignupFunction.Run must carry [RequireAuth]");
    }
}
