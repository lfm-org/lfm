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
using Lfm.Api.Runs;
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

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-user") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "User#1234",
            GuildId: null,
            GuildName: null,
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

    private static HttpRequest MakePostRequest(string rawJson)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        httpContext.Request.ContentType = "application/json";
        return httpContext.Request;
    }

    private static RunsSignupFunction MakeFunction(
        Mock<IRunSignupService> service,
        TestLogger<RunsSignupFunction>? logger = null)
    {
        return new RunsSignupFunction(
            service.Object,
            logger ?? new TestLogger<RunsSignupFunction>());
    }

    private static RunDocument MakeRunDoc(
        string id = "run-1",
        IReadOnlyList<RunCharacterEntry>? runCharacters = null) =>
        new RunDocument(
            Id: id,
            StartTime: DateTimeOffset.UtcNow.AddHours(24).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(22).ToString("o"),
            Description: "Test run",
            ModeKey: "NORMAL:10",
            Visibility: "GUILD",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: runCharacters ?? [
                new RunCharacterEntry(
                    Id: "entry-1",
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
            ]);

    private static object MakeBody(
        string characterId = "char-1",
        string desiredAttendance = "IN") =>
        new { characterId, desiredAttendance, specId = (int?)null };

    // ------------------------------------------------------------------
    // Test 1: Service Ok happy path → 200 with mapped DTO + audit
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_200_when_service_returns_ok()
    {
        var principal = MakePrincipal("bnet-user");
        var persisted = MakeRunDoc();

        var service = new Mock<IRunSignupService>();
        service.Setup(s => s.SignupAsync(
                "run-1",
                It.IsAny<SignupRequest>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(persisted));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(MakeBody()), "run-1", ctx, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RunDetailDto>(ok.Value);
        Assert.Single(dto.RunCharacters);
        Assert.True(dto.RunCharacters[0].IsCurrentUser);

        service.Verify(
            s => s.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Audit success entry on Ok path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_signup_create_audit_event_on_success()
    {
        var principal = MakePrincipal("bnet-user");
        var persisted = MakeRunDoc();
        var logger = new TestLogger<RunsSignupFunction>();

        var service = new Mock<IRunSignupService>();
        service.Setup(s => s.SignupAsync(
                It.IsAny<string>(),
                It.IsAny<SignupRequest>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(persisted));

        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakePostRequest(MakeBody()), "run-1", ctx, CancellationToken.None);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "signup.create",
            actorId: "bnet-user",
            result: "success"));
    }

    // ------------------------------------------------------------------
    // Test 3: Service NotFound → 404 problem+json
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_service_returns_not_found()
    {
        var principal = MakePrincipal("bnet-user");

        var service = new Mock<IRunSignupService>();
        service.Setup(s => s.SignupAsync(
                It.IsAny<string>(),
                It.IsAny<SignupRequest>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.NotFound("run-not-found", "Run not found."));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(MakeBody()), "missing-run", ctx, CancellationToken.None);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#run-not-found", problem.Type);
    }

    // ------------------------------------------------------------------
    // Test 4: Service Forbidden → 403 problem+json + audit failure entry
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_and_emits_audit_when_service_returns_forbidden()
    {
        var principal = MakePrincipal("bnet-member");

        var service = new Mock<IRunSignupService>();
        service.Setup(s => s.SignupAsync(
                It.IsAny<string>(),
                It.IsAny<SignupRequest>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Forbidden(
                "guild-rank-denied",
                "Guild signup is not enabled for your rank.",
                AuditReason: "guild rank denied"));

        var logger = new TestLogger<RunsSignupFunction>();
        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(MakeBody()), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#guild-rank-denied", problem.Type);

        Assert.Single(
            logger.Entries,
            e => e.IsAudit("signup.create", "failure", "guild rank denied"));
    }

    // ------------------------------------------------------------------
    // Test 5: Service ConflictResult → 409 problem+json (no audit)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_409_when_service_returns_conflict()
    {
        var principal = MakePrincipal("bnet-user");

        var service = new Mock<IRunSignupService>();
        service.Setup(s => s.SignupAsync(
                It.IsAny<string>(),
                It.IsAny<SignupRequest>(),
                It.IsAny<SessionPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.ConflictResult(
                "concurrent-modification",
                "Concurrent modification, please retry."));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(MakeBody()), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#concurrent-modification", problem.Type);
    }

    // ------------------------------------------------------------------
    // Test 6: Validation failure — service is never called
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_400_when_body_fails_validation()
    {
        var principal = MakePrincipal("bnet-user");

        // Missing required fields (e.g., desiredAttendance).
        var requestBody = new { characterId = "char-1" };

        var service = new Mock<IRunSignupService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#validation-failed", problem.Type);
        Assert.True(problem.Extensions.ContainsKey("errors"));

        // Service should never be called when validation fails.
        service.Verify(
            s => s.SignupAsync(It.IsAny<string>(), It.IsAny<SignupRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        var principal = MakePrincipal("bnet-user");
        var service = new Mock<IRunSignupService>();
        var fn = MakeFunction(service);

        var result = await fn.Run(
            MakePostRequest("{ not valid json at all"),
            "run-1",
            MakeFunctionContext(principal),
            CancellationToken.None);

        var bad = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, bad.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(bad.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#invalid-body", problem.Type);

        var json = JsonSerializer.Serialize(bad.Value);
        Assert.DoesNotContain("line", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("byte", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("path:", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("position", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Request body is invalid or missing.", json);

        // Service must not have been touched for a parse failure.
        service.VerifyNoOtherCalls();
    }
}
