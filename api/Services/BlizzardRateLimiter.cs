// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Threading.RateLimiting;

namespace Lfm.Api.Services;

public sealed class BlizzardRateLimiter : IBlizzardRateLimiter, IDisposable
{
    private readonly SlidingWindowRateLimiter _limiter;
    private long _pauseUntilTicks;

    public BlizzardRateLimiter(int permitsPerSecond = 80, int queueLimit = 200)
    {
        _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitsPerSecond,
            Window = TimeSpan.FromSeconds(1),
            SegmentsPerWindow = 4,
            QueueLimit = queueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public async ValueTask<BlizzardLease> AcquireAsync(CancellationToken ct)
    {
        var pause = Interlocked.Read(ref _pauseUntilTicks);
        if (pause > DateTimeOffset.UtcNow.UtcTicks)
            return new BlizzardLease(IsAcquired: false);

        using var lease = await _limiter.AcquireAsync(permitCount: 1, ct);
        return new BlizzardLease(IsAcquired: lease.IsAcquired);
    }

    public void PauseUntil(DateTimeOffset until)
    {
        Interlocked.Exchange(ref _pauseUntilTicks, until.UtcTicks);
    }

    public TimeSpan? RemainingPause
    {
        get
        {
            var pause = Interlocked.Read(ref _pauseUntilTicks);
            var remaining = pause - DateTimeOffset.UtcNow.UtcTicks;
            return remaining > 0 ? TimeSpan.FromTicks(remaining) : null;
        }
    }

    public void Dispose() => _limiter.Dispose();
}
