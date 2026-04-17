// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Lfm.Api.Functions;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class RaiderCleanupFunctionTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static RaiderDocument MakeRaiderDoc(string battleNetId) =>
        new RaiderDocument(
            Id: battleNetId,
            BattleNetId: battleNetId,
            SelectedCharacterId: null,
            Locale: null,
            LastSeenAt: "2020-01-01T00:00:00.000Z");

    private static TimerInfo MakeTimerInfo()
    {
        var mock = new Mock<TimerInfo>();
        return mock.Object;
    }

    // ------------------------------------------------------------------
    // Test 1: Expired raiders are scrubbed then deleted, in the right order
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_scrubs_and_deletes_each_expired_raider_in_order()
    {
        var raider1 = MakeRaiderDoc("bnet-1");
        var raider2 = MakeRaiderDoc("bnet-2");
        var expiredRaiders = new List<RaiderDocument> { raider1, raider2 };

        var callLog = new List<string>();

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredRaiders);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => callLog.Add($"delete:{id}"))
            .Returns(Task.CompletedTask);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-2", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => callLog.Add($"delete:{id}"))
            .Returns(Task.CompletedTask);

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => callLog.Add($"scrub:{id}"))
            .Returns(Task.CompletedTask);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-2", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((id, _) => callLog.Add($"scrub:{id}"))
            .Returns(Task.CompletedTask);

        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, NullLogger<RaiderCleanupFunction>.Instance);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        // Both raiders scrubbed
        runsRepo.Verify(r => r.ScrubRaiderAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
        runsRepo.Verify(r => r.ScrubRaiderAsync("bnet-2", It.IsAny<CancellationToken>()), Times.Once);

        // Both raiders deleted
        raidersRepo.Verify(r => r.DeleteAsync("bnet-1", It.IsAny<CancellationToken>()), Times.Once);
        raidersRepo.Verify(r => r.DeleteAsync("bnet-2", It.IsAny<CancellationToken>()), Times.Once);

        // scrub before delete for each raider
        Assert.True(callLog.IndexOf("scrub:bnet-1") < callLog.IndexOf("delete:bnet-1"));
        Assert.True(callLog.IndexOf("scrub:bnet-2") < callLog.IndexOf("delete:bnet-2"));
    }

    // ------------------------------------------------------------------
    // Test 2: Error on one raider does not abort cleanup of the others
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_continues_with_remaining_raiders_when_one_fails()
    {
        var raider1 = MakeRaiderDoc("bnet-fail");
        var raider2 = MakeRaiderDoc("bnet-ok");
        var expiredRaiders = new List<RaiderDocument> { raider1, raider2 };

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredRaiders);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-ok", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-fail", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated scrub failure"));
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-ok", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, NullLogger<RaiderCleanupFunction>.Instance);

        // Should not throw — errors are caught per-raider and logged
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        // The successful raider was still processed
        runsRepo.Verify(r => r.ScrubRaiderAsync("bnet-ok", It.IsAny<CancellationToken>()), Times.Once);
        raidersRepo.Verify(r => r.DeleteAsync("bnet-ok", It.IsAny<CancellationToken>()), Times.Once);

        // The failed raider's delete was never called (error was in scrub)
        raidersRepo.Verify(r => r.DeleteAsync("bnet-fail", It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 3: No expired raiders — no repo mutations called
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_does_nothing_when_no_expired_raiders_found()
    {
        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RaiderDocument>());

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);

        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, NullLogger<RaiderCleanupFunction>.Instance);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        // No mutations — strict mocks would throw if ScrubRaiderAsync or DeleteAsync were called
        raidersRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        runsRepo.Verify(r => r.ScrubRaiderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ------------------------------------------------------------------
    // Test 4: Cutoff is ~90 days before now (sanity check on the calculation)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_passes_a_cutoff_approximately_90_days_in_the_past()
    {
        string? capturedCutoff = null;
        var before = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(90));

        var raidersRepo = new Mock<IRaidersRepository>();
        raidersRepo
            .Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync(Array.Empty<RaiderDocument>());

        var after = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(90));

        var runsRepo = new Mock<IRunsRepository>();
        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, NullLogger<RaiderCleanupFunction>.Instance);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        Assert.NotNull(capturedCutoff);
        var parsedCutoff = DateTimeOffset.Parse(capturedCutoff!);
        Assert.True(parsedCutoff >= before);
        Assert.True(parsedCutoff <= after.Add(TimeSpan.FromSeconds(5)));
    }

    // ------------------------------------------------------------------
    // Test 5: Audit event emitted for expired raiders
    // ------------------------------------------------------------------

    // ------------------------------------------------------------------
    // Test: summary log entry counts removed/errors and includes the
    // conditional error suffix only when errors > 0
    // ------------------------------------------------------------------

    [Fact]
    public async Task Run_logs_summary_with_removed_count_and_no_error_suffix_when_all_succeed()
    {
        var raidersList = new[] { MakeRaiderDoc("bnet-1"), MakeRaiderDoc("bnet-2") };

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo.Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(raidersList);
        raidersRepo.Setup(r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo.Setup(r => r.ScrubRaiderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<RaiderCleanupFunction>();
        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, logger);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        var summary = Assert.Single(
            logger.Entries,
            e => (e.Message ?? "").Contains("Raider cleanup: removed"));
        Assert.Equal(2, summary.Properties["Removed"]);
        Assert.Equal(string.Empty, summary.Properties["ErrorSuffix"]);
    }

    [Fact]
    public async Task Run_logs_summary_with_error_suffix_when_one_raider_fails()
    {
        var raidersList = new[] { MakeRaiderDoc("bnet-fail"), MakeRaiderDoc("bnet-ok") };

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo.Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(raidersList);
        raidersRepo.Setup(r => r.DeleteAsync("bnet-ok", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo.Setup(r => r.ScrubRaiderAsync("bnet-fail", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("scrub failed"));
        runsRepo.Setup(r => r.ScrubRaiderAsync("bnet-ok", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<RaiderCleanupFunction>();
        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, logger);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        var summary = Assert.Single(
            logger.Entries,
            e => (e.Message ?? "").Contains("Raider cleanup: removed"));
        Assert.Equal(1, summary.Properties["Removed"]);
        Assert.Equal(", 1 error(s)", summary.Properties["ErrorSuffix"]);
    }

    [Fact]
    public async Task Run_logs_summary_with_zero_removed_when_no_expired_raiders()
    {
        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo.Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RaiderDocument>());
        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);

        var logger = new TestLogger<RaiderCleanupFunction>();
        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, logger);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        var summary = Assert.Single(
            logger.Entries,
            e => (e.Message ?? "").Contains("Raider cleanup: removed"));
        Assert.Equal(0, summary.Properties["Removed"]);
        Assert.Equal(string.Empty, summary.Properties["ErrorSuffix"]);
    }

    [Fact]
    public async Task Run_emits_account_expired_audit_event()
    {
        // Arrange
        var raider = MakeRaiderDoc("bnet-42");
        var expiredRaiders = new List<RaiderDocument> { raider };

        var raidersRepo = new Mock<IRaidersRepository>(MockBehavior.Strict);
        raidersRepo
            .Setup(r => r.ListExpiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredRaiders);
        raidersRepo
            .Setup(r => r.DeleteAsync("bnet-42", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var runsRepo = new Mock<IRunsRepository>(MockBehavior.Strict);
        runsRepo
            .Setup(r => r.ScrubRaiderAsync("bnet-42", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new TestLogger<RaiderCleanupFunction>();
        var fn = new RaiderCleanupFunction(raidersRepo.Object, runsRepo.Object, logger);
        await fn.Run(MakeTimerInfo(), CancellationToken.None);

        // Assert: logger called with "account.expired" and "success"
        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "account.expired",
            actorId: "system",
            result: "success"));
    }
}
