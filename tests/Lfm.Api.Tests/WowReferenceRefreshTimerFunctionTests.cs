// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Functions;
using Lfm.Api.Services;
using Lfm.Contracts.Admin;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Covers the weekly-timer entry point. The heavy lifting is in
/// <see cref="IReferenceSync"/>, which has its own tests; this file only
/// verifies that the timer invokes the sync and logs each entity's result.
/// </summary>
public class WowReferenceRefreshTimerFunctionTests
{
    [Fact]
    public async Task Run_invokes_reference_sync_and_logs_each_entity_result()
    {
        var sync = new Mock<IReferenceSync>();
        sync.Setup(s => s.SyncAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WowUpdateResponse(new[]
            {
                new WowUpdateEntityResult("instances", "synced (211 docs)"),
                new WowUpdateEntityResult("specializations", "synced (40 docs)"),
            }));
        var logger = new TestLogger<WowReferenceRefreshTimerFunction>();
        var sut = new WowReferenceRefreshTimerFunction(sync.Object, logger);

        await sut.Run(new TimerInfo(), CancellationToken.None);

        sync.Verify(s => s.SyncAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            (e.Message ?? "").Contains("Starting weekly"));
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            (e.Message ?? "").Contains("instances") &&
            (e.Message ?? "").Contains("synced (211 docs)"));
        Assert.Contains(logger.Entries, e =>
            e.Level == LogLevel.Information &&
            (e.Message ?? "").Contains("specializations") &&
            (e.Message ?? "").Contains("synced (40 docs)"));
    }
}
