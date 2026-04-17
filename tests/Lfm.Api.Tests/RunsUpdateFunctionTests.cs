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
using Lfm.Contracts.Instances;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

public class RunsUpdateFunctionTests
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

    private static RaiderDocument MakeRaiderDoc(
        string battleNetId = "bnet-creator",
        int? guildId = 12345,
        string? guildName = "Test Guild") =>
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
                    GuildName: guildName)
            ]);

    private static Mock<IRaidersRepository> MakeRaidersRepoFor(RaiderDocument raider)
    {
        var m = new Mock<IRaidersRepository>();
        m.Setup(r => r.GetByBattleNetIdAsync(raider.BattleNetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(raider);
        return m;
    }

    private static HttpRequest MakePutRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        return httpContext.Request;
    }

    /// <summary>
    /// A run that is open for editing: startTime is 24 hours in the future,
    /// signupCloseTime is 2 hours before that, no signups yet.
    /// </summary>
    private static RunDocument MakeOpenRunDoc(
        string id = "run-1",
        string creatorBattleNetId = "bnet-creator",
        int? creatorGuildId = 12345,
        string visibility = "PUBLIC") =>
        new RunDocument(
            Id: id,
            StartTime: DateTimeOffset.UtcNow.AddHours(24).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(22).ToString("o"),
            Description: "Original description",
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

    private static RunsUpdateFunction MakeFunction(
        Mock<IRunsRepository> repo,
        Mock<IGuildPermissions> permissions,
        Mock<IInstancesRepository> instancesRepo,
        Mock<IRaidersRepository>? raidersRepo = null,
        TestLogger<RunsUpdateFunction>? logger = null)
    {
        return new RunsUpdateFunction(
            repo.Object,
            (raidersRepo ?? new Mock<IRaidersRepository>()).Object,
            permissions.Object,
            instancesRepo.Object,
            logger ?? new TestLogger<RunsUpdateFunction>());
    }

    private static IReadOnlyList<InstanceDto> MakeInstances() =>
        new List<InstanceDto>
        {
            new("631", "Icecrown Citadel", "NORMAL:10", "wrath"),
            new("631", "Icecrown Citadel", "HEROIC:25", "wrath"),
        };

    // ------------------------------------------------------------------
    // Test 1: Admin (creator) happy path — updates run and returns 200
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_updates_run_and_returns_200_for_creator()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");
        var existing = MakeOpenRunDoc(creatorBattleNetId: "bnet-creator");
        var updatedDoc = existing with { Description = "Updated description" };

        var requestBody = new { description = "Updated description" };

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDoc);

        var permissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();
        instancesRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstances());

        var raidersRepo = MakeRaidersRepoFor(MakeRaiderDoc("bnet-creator"));

        var fn = MakeFunction(repo, permissions, instancesRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(requestBody), "run-1", ctx, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<RunDetailDto>(okResult.Value);

        // Cosmos UpdateAsync was called once
        repo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------
    // Test 2: Raider not found — returns 404
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_404_when_raider_not_found()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");
        var existing = MakeOpenRunDoc(creatorBattleNetId: "bnet-creator");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo.Setup(r => r.GetByBattleNetIdAsync("bnet-creator", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RaiderDocument?)null);

        var permissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();

        var fn = MakeFunction(repo, permissions, instancesRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Updated" }), "run-1", ctx, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFound.Value);
        var errorProp = notFound.Value!.GetType().GetProperty("error");
        Assert.NotNull(errorProp);
        Assert.Equal("Raider not found", errorProp!.GetValue(notFound.Value));

        repo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var instancesRepo = new Mock<IInstancesRepository>();

        var fn = MakeFunction(repo, permissions, instancesRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { }), "missing-run", ctx, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);

        // Cosmos UpdateAsync must never be called
        repo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: Non-creator with no guild relationship — returns 403
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_403_for_non_creator_without_guild_permission()
    {
        // Caller belongs to guild 99999 — different from the run's creator guild (12345).
        var principal = MakePrincipal(battleNetId: "bnet-other", guildId: "99999");
        var existing = MakeOpenRunDoc(creatorBattleNetId: "bnet-creator", creatorGuildId: 12345, visibility: "PUBLIC");

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var permissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();
        var logger = new TestLogger<RunsUpdateFunction>();

        // Raider's selected character is in guild 99999 — different from the run's creator guild.
        var raidersRepo = MakeRaidersRepoFor(MakeRaiderDoc("bnet-other", guildId: 99999));

        var fn = MakeFunction(repo, permissions, instancesRepo, raidersRepo, logger: logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Hacked" }), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, objectResult.StatusCode);

        repo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Single(
            logger.Entries,
            e => e.IsAudit("run.update", "failure", "not creator"));
    }

    // ------------------------------------------------------------------
    // Test 4: Editing closed (run start time has passed) — returns 409 Conflict
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_returns_409_when_editing_is_closed()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");

        // Run whose startTime is in the past → editing closed.
        var pastRun = new RunDocument(
            Id: "run-1",
            StartTime: DateTimeOffset.UtcNow.AddHours(-1).ToString("o"),
            SignupCloseTime: DateTimeOffset.UtcNow.AddHours(-2).ToString("o"),
            Description: "Past run",
            ModeKey: "NORMAL:10",
            Visibility: "PUBLIC",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 631,
            InstanceName: "Icecrown Citadel",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-14).ToString("o"),
            Ttl: 86400,
            RunCharacters: []);

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pastRun);

        var permissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();
        var raidersRepo = MakeRaidersRepoFor(MakeRaiderDoc("bnet-creator"));

        var fn = MakeFunction(repo, permissions, instancesRepo, raidersRepo);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(MakePutRequest(new { description = "Too late" }), "run-1", ctx, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, objectResult.StatusCode);

        repo.Verify(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 5: Audit event emitted on success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_run_update_audit_event_on_success()
    {
        var principal = MakePrincipal(battleNetId: "bnet-creator");
        var existing = MakeOpenRunDoc(creatorBattleNetId: "bnet-creator");
        var updatedDoc = existing with { Description = "Updated description" };
        var logger = new TestLogger<RunsUpdateFunction>();

        var requestBody = new { description = "Updated description" };

        var repo = new Mock<IRunsRepository>();
        repo.Setup(r => r.GetByIdAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<RunDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDoc);

        var permissions = new Mock<IGuildPermissions>();
        var instancesRepo = new Mock<IInstancesRepository>();
        instancesRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeInstances());

        var raidersRepo = MakeRaidersRepoFor(MakeRaiderDoc("bnet-creator"));

        var fn = MakeFunction(repo, permissions, instancesRepo, raidersRepo, logger: logger);
        var ctx = MakeFunctionContext(principal);

        await fn.Run(MakePutRequest(requestBody), "run-1", ctx, CancellationToken.None);

        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "run.update",
            actorId: "bnet-creator",
            result: "success"));
    }
}
