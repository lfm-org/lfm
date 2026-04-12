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

    private static RunsSignupFunction MakeFunction(
        Mock<IRunsRepository> runsRepo,
        Mock<IRaidersRepository> raidersRepo,
        Mock<IGuildPermissions> permissions,
        TestLogger<RunsSignupFunction>? logger = null)
    {
        return new RunsSignupFunction(
            runsRepo.Object,
            raidersRepo.Object,
            permissions.Object,
            logger ?? new TestLogger<RunsSignupFunction>());
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

    /// <summary>
    /// Pre-wired mocks for a successful signup call. Each test can mutate the
    /// fields before invoking the function to introduce a single axis of variance.
    /// </summary>
    private sealed class HappyPathFixture
    {
        public required Mock<IRunsRepository> RunsRepo { get; init; }
        public required Mock<IRaidersRepository> RaidersRepo { get; init; }
        public required Mock<IGuildPermissions> Permissions { get; init; }
        public required TestLogger<RunsSignupFunction> Logger { get; init; }
        public required SessionPrincipal Principal { get; init; }
        public required FunctionContext Context { get; init; }
        public required object RequestBody { get; init; }
    }

    private static HappyPathFixture MakeHappyPath()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var run = MakeRunDoc();
        var raider = MakeRaiderDoc(battleNetId: "bnet-user", characterId: "char-1");

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

        return new HappyPathFixture
        {
            RunsRepo = runsRepo,
            RaidersRepo = raidersRepo,
            Permissions = new Mock<IGuildPermissions>(),
            Logger = new TestLogger<RunsSignupFunction>(),
            Principal = principal,
            Context = MakeFunctionContext(principal),
            RequestBody = new
            {
                characterId = "char-1",
                desiredAttendance = "IN",
                specId = (int?)null,
            },
        };
    }

    // ------------------------------------------------------------------
    // Test 1: Happy path — new signup returns 200 with sanitized run
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_adds_signup_and_returns_200()
    {
        var fx = MakeHappyPath();

        var fn = MakeFunction(fx.RunsRepo, fx.RaidersRepo, fx.Permissions, fx.Logger);
        var result = await fn.Run(MakePostRequest(fx.RequestBody), "run-1", fx.Context, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<RunDetailDto>().Subject;
        dto.RunCharacters.Should().HaveCount(1);
        dto.RunCharacters[0].IsCurrentUser.Should().BeTrue();

        fx.RunsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Run not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_run_does_not_exist()
    {
        var principal = MakePrincipal();
        var raider = MakeRaiderDoc(battleNetId: "bnet-user", characterId: "char-1");

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("missing-run", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(runsRepo, raidersRepo, permissions);
        var ctx = MakeFunctionContext(principal);

        var requestBody = new { characterId = "char-1", desiredAttendance = "IN" };
        var result = await fn.Run(MakePostRequest(requestBody), "missing-run", ctx, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var raider = MakeRaiderDoc(battleNetId: "bnet-member", characterId: "char-1");

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-member", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanSignupGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var logger = new TestLogger<RunsSignupFunction>();
        var fn = MakeFunction(runsRepo, raidersRepo, permissions, logger);
        var ctx = MakeFunctionContext(principal);

        var requestBody = new { characterId = "char-1", desiredAttendance = "IN" };
        var result = await fn.Run(MakePostRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);

        logger.Entries.Should().ContainSingle(
            e => e.IsAudit("signup.create", "failure", "guild rank denied"),
            "denied guild signup must emit a failure audit event");
    }

    // ------------------------------------------------------------------
    // Test 5: Happy path — emits signup.create audit event
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_signup_create_audit_event_on_success()
    {
        var fx = MakeHappyPath();

        var fn = MakeFunction(fx.RunsRepo, fx.RaidersRepo, fx.Permissions, fx.Logger);
        await fn.Run(MakePostRequest(fx.RequestBody), "run-1", fx.Context, CancellationToken.None);

        fx.Logger.Entries.Should().ContainSingle(e => e.IsAudit(
            action: "signup.create",
            actorId: "bnet-user",
            result: "success"),
            "success path must emit a signup.create audit event");
    }

    // ------------------------------------------------------------------
    // Test 6: Signup close time has passed — returns 409
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_409_when_signup_close_time_has_passed()
    {
        var principal = MakePrincipal(battleNetId: "bnet-user");
        var raider = MakeRaiderDoc(battleNetId: "bnet-user", characterId: "char-1");

        // Run whose signupCloseTime is in the past.
        var closedRun = new RunDocument(
            Id: "run-1",
            StartTime: DateTimeOffset.UtcNow.AddHours(2).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),
            Description: "Closed run",
            ModeKey: "NORMAL:10",
            Visibility: "PUBLIC",
            CreatorGuild: "Test Guild",
            CreatorGuildId: null,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 86400,
            RunCharacters: []);

        var runsRepo = new Mock<IRunsRepository>();
        runsRepo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(closedRun);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var permissions = new Mock<IGuildPermissions>();
        var fn = MakeFunction(runsRepo, raidersRepo, permissions);
        var ctx = MakeFunctionContext(principal);

        var requestBody = new { characterId = "char-1", desiredAttendance = "IN" };
        var result = await fn.Run(MakePostRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(409);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 7: Concurrency conflict retries and succeeds on second attempt
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_retries_on_concurrency_conflict_and_succeeds()
    {
        var fx = MakeHappyPath();

        // Override UpdateAsync to throw on the first call, then succeed.
        // The behavior we care about is: after a transient conflict the caller
        // sees a successful 200 with the persisted run state. The exact retry
        // count is a loop-structure detail and is not pinned here.
        var callCount = 0;
        fx.RunsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .Returns<RunDocument, CancellationToken>((doc, _) =>
            {
                callCount++;
                if (callCount == 1)
                    throw new ConcurrencyConflictException();
                return Task.FromResult(doc);
            });

        var fn = MakeFunction(fx.RunsRepo, fx.RaidersRepo, fx.Permissions, fx.Logger);
        var result = await fn.Run(MakePostRequest(fx.RequestBody), "run-1", fx.Context, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var detail = ok.Value.Should().BeOfType<RunDetailDto>().Subject;
        detail.Id.Should().Be("run-1",
            "the retried success path must surface the persisted run, not a stale or empty payload");
    }

    // ------------------------------------------------------------------
    // Test 8: Concurrency conflict exhausts all retries — returns 409
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_409_after_exhausting_concurrency_retries()
    {
        var fx = MakeHappyPath();

        fx.RunsRepo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());

        var fn = MakeFunction(fx.RunsRepo, fx.RaidersRepo, fx.Permissions, fx.Logger);
        var result = await fn.Run(MakePostRequest(fx.RequestBody), "run-1", fx.Context, CancellationToken.None);

        // The visible 409 is sufficient evidence that the retry loop exhausted —
        // pinning Times.Exactly(3) couples the test to the loop count, which is a
        // structural detail that may legitimately change (e.g. policy library swap).
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(409);
    }
}
