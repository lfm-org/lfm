using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsCreateFunctionTests
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

    private static RunsCreateFunction MakeFunction(
        Mock<IRunsRepository> repo,
        Mock<IGuildPermissions> permissions,
        Mock<ILogger<RunsCreateFunction>>? loggerMock = null)
    {
        var logger = (loggerMock ?? new Mock<ILogger<RunsCreateFunction>>()).Object;
        return new RunsCreateFunction(repo.Object, permissions.Object, logger);
    }

    private static RunDocument MakeRunDoc(string id = "run-new") =>
        new RunDocument(
            Id: id,
            StartTime: "2026-06-01T20:00:00Z",
            SignupCloseTime: "2026-06-01T18:00:00Z",
            Description: "Created run",
            ModeKey: "NORMAL:10",
            Visibility: "PUBLIC",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-admin",
            CreatedAt: "2026-05-01T00:00:00Z",
            Ttl: 604800,
            RunCharacters: []);

    // ------------------------------------------------------------------
    // Test 1: Admin happy path — creates run and returns 201
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_creates_run_and_returns_201_for_guild_admin()
    {
        var principal = MakePrincipal(battleNetId: "bnet-admin", guildId: "12345");
        var created = MakeRunDoc("run-new");

        var requestBody = new
        {
            startTime = "2026-06-01T20:00:00Z",
            signupCloseTime = "2026-06-01T18:00:00Z",
            description = "Created run",
            modeKey = "NORMAL:10",
            visibility = "GUILD",
            instanceId = 631,
            instanceName = "Icecrown Citadel",
        };

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanCreateGuildRunsAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        objectResult.Value.Should().BeOfType<RunDetailDto>();

        // Cosmos CreateAsync was called once
        repo.Verify(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Validation failure — returns 400 with error details
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_400_when_body_fails_validation()
    {
        var principal = MakePrincipal();

        // Missing required fields: modeKey, visibility, instanceId
        var requestBody = new
        {
            startTime = "2026-06-01T20:00:00Z",
        };

        var repo = new Mock<IRunsRepository>();
        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();

        // Cosmos should never be called
        repo.Verify(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: Non-admin — returns 403 for GUILD run
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_for_guild_run_when_caller_lacks_permission()
    {
        var principal = MakePrincipal(battleNetId: "bnet-member", guildId: "12345");

        var requestBody = new
        {
            startTime = "2026-06-01T20:00:00Z",
            modeKey = "NORMAL:10",
            visibility = "GUILD",
            instanceId = 631,
        };

        var repo = new Mock<IRunsRepository>();
        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanCreateGuildRunsAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);

        repo.Verify(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 4: [RequireAuth] attribute is present on Run method
    // ------------------------------------------------------------------

    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(RunsCreateFunction).GetMethod(nameof(RunsCreateFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "RunsCreateFunction.Run must carry [RequireAuth]");
    }

    // ------------------------------------------------------------------
    // Test 5: Audit event emitted on success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_run_create_audit_event_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-admin", guildId: "12345");
        var created = MakeRunDoc("run-new");
        var loggerMock = new Mock<ILogger<RunsCreateFunction>>();

        var requestBody = new
        {
            startTime = "2026-06-01T20:00:00Z",
            signupCloseTime = "2026-06-01T18:00:00Z",
            description = "Created run",
            modeKey = "NORMAL:10",
            visibility = "GUILD",
            instanceId = 631,
            instanceName = "Icecrown Citadel",
        };

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.CreateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanCreateGuildRunsAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var fn = MakeFunction(repo, permissions, loggerMock);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakePostRequest(requestBody), ctx, CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("run.create") &&
                    v.ToString()!.Contains("bnet-admin") &&
                    v.ToString()!.Contains("success")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "success path must emit a run.create audit event with the battleNetId and result");
    }
}
