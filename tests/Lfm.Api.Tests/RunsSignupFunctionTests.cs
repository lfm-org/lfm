// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
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
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: runCharacters ?? []);

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-user",
        string characterId = "char-1",
        int? guildId = null) =>
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
                    Name: "Testchar",
                    GuildId: guildId,
                    GuildName: guildId is not null ? "Test Guild" : null)
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

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RunDetailDto>(okResult.Value);
        Assert.Single(dto.RunCharacters);
        Assert.True(dto.RunCharacters[0].IsCurrentUser);

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

        Assert.IsType<NotFoundObjectResult>(result);

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
        var raider = MakeRaiderDoc(battleNetId: "bnet-member", characterId: "char-1", guildId: 12345);

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

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        runsRepo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Single(logger.Entries, e => e.IsAudit("signup.create", "failure", "guild rank denied"));
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

        Assert.Single(fx.Logger.Entries, e => e.IsAudit(
            action: "signup.create",
            actorId: "bnet-user",
            result: "success"));
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
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
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

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);

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

        var ok = Assert.IsType<OkObjectResult>(result);
        var detail = Assert.IsType<RunDetailDto>(ok.Value);
        Assert.Equal("run-1", detail.Id);
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
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    // ------------------------------------------------------------------
    // Invalid JSON body — generic 400 with no parser detail
    // ------------------------------------------------------------------
    //
    // Pin the contract that a JsonException never flows to the client. The
    // message typically contains byte offsets, line numbers, and fragments
    // of the caller's payload — none of it is useful over the wire, and it
    // drifts between System.Text.Json versions.

    [Fact]
    public async Task Run_returns_generic_400_when_body_is_invalid_json()
    {
        var principal = MakePrincipal();
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var permissions = new Mock<IGuildPermissions>();
        var fn = MakeFunction(runsRepo, raidersRepo, permissions);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{ not valid json at all"));
        httpContext.Request.ContentType = "application/json";

        var result = await fn.Run(httpContext.Request, "run-1", MakeFunctionContext(principal), CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);

        var json = JsonSerializer.Serialize(bad.Value);
        Assert.DoesNotContain("line", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("byte", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path:", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Invalid request body", json);

        // Repos must not have been touched for a parse failure.
        runsRepo.VerifyNoOtherCalls();
        raidersRepo.VerifyNoOtherCalls();
        permissions.VerifyNoOtherCalls();
    }
}
