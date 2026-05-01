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

public class RunsCreateFunctionTests
{
    // Anchored to UtcNow so these fixtures never become time bombs against a
    // future-dated assertion. See issue #49.
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");
    private static readonly string PastCreatedAt =
        DateTimeOffset.UtcNow.AddDays(-14).ToString("o");

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
        string battleNetId = "bnet-admin",
        string? guildId = "12345",
        string? guildName = "Test Guild") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Admin#1234",
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

    private static HttpRequest MakePostRequest(string rawJson)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        httpContext.Request.ContentType = "application/json";
        return httpContext.Request;
    }

    private static RunsCreateFunction MakeFunction(
        Mock<IRunCreateService> service,
        TestLogger<RunsCreateFunction>? logger = null)
    {
        return new RunsCreateFunction(
            service.Object,
            logger ?? new TestLogger<RunsCreateFunction>());
    }

    private static RunDocument MakeRunDoc(string id = "run-new") =>
        new RunDocument(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Created run",
            ModeKey: "NORMAL:10",
            Visibility: "GUILD",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-admin",
            CreatedAt: PastCreatedAt,
            Ttl: 604800,
            RunCharacters: []);

    // ------------------------------------------------------------------
    // Test 1: Service Ok happy path → 201 Created with mapped DTO
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_creates_run_and_returns_201_when_service_returns_ok()
    {
        var principal = MakePrincipal(battleNetId: "bnet-admin", guildId: "12345");
        var created = MakeRunDoc("run-new");

        var requestBody = new
        {
            startTime = FutureStartTime,
            signupCloseTime = FutureSignupCloseTime,
            description = "Created run",
            difficulty = "NORMAL",
            size = 10,
            visibility = "GUILD",
            instanceId = 631,
            instanceName = "Icecrown Citadel",
        };

        var service = new Mock<IRunCreateService>();
        service.Setup(s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(created));

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        Assert.IsType<RunDetailDto>(objectResult.Value);

        service.Verify(
            s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Validation failure — returns 400 with error details
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_400_when_body_fails_validation()
    {
        var principal = MakePrincipal();

        // Missing required fields.
        var requestBody = new
        {
            startTime = FutureStartTime,
        };

        var service = new Mock<IRunCreateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#validation-failed", problem.Type);
        Assert.True(problem.Extensions.ContainsKey("errors"));

        // Service should never be called when validation fails.
        service.Verify(
            s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_returns_400_when_body_is_malformed_json()
    {
        var principal = MakePrincipal();
        var service = new Mock<IRunCreateService>();

        var fn = MakeFunction(service);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest("{"), ctx, CancellationToken.None);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#invalid-body", problem.Type);
        Assert.Equal("Request body is invalid or missing.", problem.Detail);

        service.Verify(
            s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: Service Forbidden → 403 problem+json + audit failure entry
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_and_emits_audit_when_service_returns_forbidden()
    {
        var principal = MakePrincipal(battleNetId: "bnet-member", guildId: "12345");

        var requestBody = new
        {
            startTime = FutureStartTime,
            difficulty = "NORMAL",
            size = 10,
            visibility = "GUILD",
            instanceId = 631,
        };

        var service = new Mock<IRunCreateService>();
        service.Setup(s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Forbidden(
                "guild-rank-denied",
                "Guild run creation is not enabled for your rank.",
                AuditReason: "guild rank denied"));

        var logger = new TestLogger<RunsCreateFunction>();
        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#guild-rank-denied", problem.Type);

        Assert.Single(
            logger.Entries,
            e => e.IsAudit("run.create", "failure", "guild rank denied"));
    }

    // ------------------------------------------------------------------
    // Test 4: Audit success entry on Ok path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_run_create_audit_event_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-admin", guildId: "12345");
        var created = MakeRunDoc("run-new");
        var logger = new TestLogger<RunsCreateFunction>();

        var requestBody = new
        {
            startTime = FutureStartTime,
            signupCloseTime = FutureSignupCloseTime,
            description = "Created run",
            difficulty = "NORMAL",
            size = 10,
            visibility = "GUILD",
            instanceId = 631,
            instanceName = "Icecrown Citadel",
        };

        var service = new Mock<IRunCreateService>();
        service.Setup(s => s.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunOperationResult.Ok(created));

        var fn = MakeFunction(service, logger);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "run.create",
            actorId: "bnet-admin",
            result: "success"));
    }
}
