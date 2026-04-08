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
using Xunit;

namespace Lfm.Api.Tests;

public class RunsDeleteFunctionTests
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
        string battleNetId = "bnet-creator",
        string? guildId = "12345",
        string? guildName = "Test Guild") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Creator#1234",
            GuildId: guildId,
            GuildName: guildName,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static RunsDeleteFunction MakeFunction(
        Mock<IRunsRepository> repo,
        Mock<IGuildPermissions> permissions,
        Mock<ILogger<RunsDeleteFunction>>? loggerMock = null)
    {
        var logger = (loggerMock ?? new Mock<ILogger<RunsDeleteFunction>>()).Object;
        return new RunsDeleteFunction(repo.Object, permissions.Object, logger);
    }

    private static RunDocument MakeRunDoc(
        string id = "run-1",
        string creatorBattleNetId = "bnet-creator",
        int? creatorGuildId = 12345,
        string visibility = "PUBLIC") =>
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
            CreatorBattleNetId: creatorBattleNetId,
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 86400,
            RunCharacters: []);

    // ------------------------------------------------------------------
    // Test 1: Creator deletes own run — returns 200 { deleted: true }
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_deletes_run_and_returns_200_for_creator()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");
        var existing = MakeRunDoc(creatorBattleNetId: "bnet-creator");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.DeleteAsync("run-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        var result = await fn.Run(req, "run-1", ctx, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new { deleted = true });

        repo.Verify(r => r.DeleteAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        permissions.Verify(p => p.CanDeleteGuildRunsAsync(It.IsAny<SessionPrincipal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 2: Non-creator without guild permission — returns 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_for_non_creator_without_guild_permission()
    {
        // Caller belongs to guild 12345 (same as run) but lacks canDeleteGuildRuns.
        var principal = MakePrincipal(battleNetId: "bnet-other", guildId: "12345");
        var existing = MakeRunDoc(
            creatorBattleNetId: "bnet-creator",
            creatorGuildId: 12345,
            visibility: "GUILD");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanDeleteGuildRunsAsync(principal, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        var result = await fn.Run(req, "run-1", ctx, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);

        repo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: Run not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_run_does_not_exist()
    {
        var principal = MakePrincipal();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("missing-run", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDocument?)null);

        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(repo, permissions);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        var result = await fn.Run(req, "missing-run", ctx, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();

        repo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 4: Audit event emitted on success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_run_delete_audit_event_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");
        var existing = MakeRunDoc(creatorBattleNetId: "bnet-creator");
        var loggerMock = new Mock<ILogger<RunsDeleteFunction>>();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.DeleteAsync("run-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(repo, permissions, loggerMock);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        await fn.Run(req, "run-1", ctx, CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("run.delete") &&
                    v.ToString()!.Contains("bnet-creator") &&
                    v.ToString()!.Contains("success")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "success path must emit a run.delete audit event with the battleNetId and result");
    }
}
