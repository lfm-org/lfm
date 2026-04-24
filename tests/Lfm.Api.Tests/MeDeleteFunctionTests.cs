// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class MeDeleteFunctionTests
{
    // Mirrors the helper in MeFunctionTests.
    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static MeDeleteFunction MakeFunction(TestLogger<MeDeleteFunction>? logger = null)
    {
        var runsRepo = new Mock<IRunsRepository>();
        var raidersRepo = new Mock<IRaidersRepository>();
        var idempotency = new Mock<Lfm.Api.Services.IIdempotencyStore>();
        return new MeDeleteFunction(
            runsRepo.Object,
            raidersRepo.Object,
            idempotency.Object,
            logger ?? new TestLogger<MeDeleteFunction>());
    }

    [Fact]
    public async Task Returns_ok_and_calls_both_repos_in_order_when_raider_exists()
    {
        // Data-safety invariant: runs MUST be scrubbed before the raider document is
        // deleted, so a half-completed delete cannot leave dangling FK references.
        // Use Moq's MockSequence to enforce ordering instead of a manual callOrder list.
        var principal = MakePrincipal("bnet-1");
        var sequence = new MockSequence();

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo.InSequence(sequence)
            .Setup(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo.InSequence(sequence)
            .Setup(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<MeDeleteFunction>();
        var fn = new MeDeleteFunction(runsRepo.Object, raidersRepo.Object, new Mock<Lfm.Api.Services.IIdempotencyStore>().Object, logger);
        var ctx = MakeFunctionContext(principal);

        var result = await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        runsRepo.Verify(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
        raidersRepo.Verify(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Audit events
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Run_emits_account_delete_audit_event()
    {
        // Arrange
        var principal = MakePrincipal("bnet-42");
        var logger = new TestLogger<MeDeleteFunction>();
        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-42", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-42", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var fn = new MeDeleteFunction(runsRepo.Object, raidersRepo.Object, new Mock<Lfm.Api.Services.IIdempotencyStore>().Object, logger);
        var ctx = MakeFunctionContext(principal);

        // Act
        await fn.Run(new DefaultHttpContext().Request, ctx, CancellationToken.None);

        // Assert: logger called with "account.delete" and "success"
        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "account.delete",
            actorId: "bnet-42",
            result: "success"));
    }

}
