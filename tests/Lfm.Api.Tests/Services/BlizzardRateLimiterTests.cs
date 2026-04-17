// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class BlizzardRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_succeeds_within_capacity()
    {
        var limiter = new BlizzardRateLimiter(permitsPerSecond: 10, queueLimit: 5);
        using var lease = await limiter.AcquireAsync(CancellationToken.None);
        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task AcquireAsync_queues_beyond_capacity_and_drains()
    {
        var limiter = new BlizzardRateLimiter(permitsPerSecond: 2, queueLimit: 10);
        var leases = new List<Task<BlizzardLease>>();
        for (var i = 0; i < 5; i++)
            leases.Add(limiter.AcquireAsync(CancellationToken.None).AsTask());
        var results = await Task.WhenAll(leases);
        Assert.All(results, l => Assert.True(l.IsAcquired));
    }

    [Fact]
    public async Task PauseUntil_short_circuits_subsequent_acquisitions()
    {
        var limiter = new BlizzardRateLimiter(permitsPerSecond: 100, queueLimit: 10);
        limiter.PauseUntil(DateTimeOffset.UtcNow.AddMilliseconds(200));
        using var lease = await limiter.AcquireAsync(CancellationToken.None);
        Assert.False(lease.IsAcquired);
    }

    [Fact]
    public async Task PauseUntil_expires_after_window()
    {
        var limiter = new BlizzardRateLimiter(permitsPerSecond: 100, queueLimit: 10);
        limiter.PauseUntil(DateTimeOffset.UtcNow.AddMilliseconds(50));
        await Task.Delay(100);
        using var lease = await limiter.AcquireAsync(CancellationToken.None);
        Assert.True(lease.IsAcquired);
    }
}
