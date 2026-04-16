// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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

    private static RaiderDocument MakeRaiderDoc(string battleNetId = "bnet-creator") =>
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
                    GuildId: 12345,
                    GuildName: "Test Guild")
            ]);

    private static RunsDeleteFunction MakeFunction(
        Mock<IRunsRepository> repo,
        Mock<IGuildPermissions> permissions,
        Mock<IRaidersRepository>? raidersRepo = null,
        TestLogger<RunsDeleteFunction>? logger = null)
    {
        return new RunsDeleteFunction(
            repo.Object,
            (raidersRepo ?? new Mock<IRaidersRepository>()).Object,
            permissions.Object,
            logger ?? new TestLogger<RunsDeleteFunction>());
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
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
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

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var deletedProp = okResult.Value!.GetType().GetProperty("deleted");
        Assert.NotNull(deletedProp);
        Assert.Equal(true, deletedProp!.GetValue(okResult.Value));

        repo.Verify(r => r.DeleteAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        permissions.Verify(p => p.CanDeleteGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 2: Non-creator without guild permission — returns 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_for_non_creator_without_guild_permission()
    {
        // Caller belongs to guild 12345 (same as run) but lacks canDeleteGuildRuns.
        var principal = MakePrincipal(battleNetId: "bnet-other", guildId: "12345");
        var raider = MakeRaiderDoc("bnet-other");
        var existing = MakeRunDoc(
            creatorBattleNetId: "bnet-creator",
            creatorGuildId: 12345,
            visibility: "GUILD");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-other", It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);

        var permissions = new Mock<IGuildPermissions>();
        permissions.Setup(p => p.CanDeleteGuildRunsAsync(It.IsAny<RaiderDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var logger = new TestLogger<RunsDeleteFunction>();
        var fn = MakeFunction(repo, permissions, raidersRepo, logger);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        var result = await fn.Run(req, "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        repo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Single(
            logger.Entries,
            e => e.IsAudit("run.delete", "failure", "guild rank denied"));
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

        Assert.IsType<NotFoundObjectResult>(result);

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
        var logger = new TestLogger<RunsDeleteFunction>();

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.DeleteAsync("run-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var permissions = new Mock<IGuildPermissions>();

        var fn = MakeFunction(repo, permissions, logger: logger);
        var ctx = MakeFunctionContext(principal);
        var req = new DefaultHttpContext().Request;

        await fn.Run(req, "run-1", ctx, CancellationToken.None);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "run.delete",
            actorId: "bnet-creator",
            result: "success"));
    }
}
