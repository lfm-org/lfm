// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Phase 1 stub coverage. Both reference-sync entity writers throw
/// <see cref="NotImplementedException"/> until Phase 3 rewires them to upload
/// blobs instead of upserting Cosmos. The writer contract we defend here is
/// exactly that: the admin endpoint surfaces a clear per-entity "failed:"
/// status rather than aborting the whole call, and each failure is logged at
/// Error level.
/// </summary>
public class ReferenceSyncTests
{
    [Fact]
    public async Task SyncAllAsync_reports_both_entities_as_failed_pending_phase_3_blob_writer()
    {
        var logger = new TestLogger<ReferenceSync>();
        var sut = new ReferenceSync(logger);

        var response = await sut.SyncAllAsync(CancellationToken.None);

        Assert.Collection(response.Results,
            first =>
            {
                Assert.Equal("instances", first.Name);
                Assert.StartsWith("failed:", first.Status);
            },
            second =>
            {
                Assert.Equal("specializations", second.Name);
                Assert.StartsWith("failed:", second.Status);
            });

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("instances"));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && (e.Message ?? "").Contains("specializations"));
    }
}
