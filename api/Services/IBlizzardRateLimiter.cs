// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

public readonly record struct BlizzardLease(bool IsAcquired) : IDisposable
{
    public void Dispose() { }
}

public interface IBlizzardRateLimiter
{
    ValueTask<BlizzardLease> AcquireAsync(CancellationToken ct);
    void PauseUntil(DateTimeOffset until);

    /// <summary>
    /// How long the rate limiter is currently pausing outgoing Blizzard calls,
    /// if a pause is in effect; null otherwise. Used by callers that surface
    /// 429 to the browser so they can echo a RFC 9110 <c>Retry-After</c>
    /// header. The value is the pause budget remaining from the limiter's
    /// last <see cref="PauseUntil"/> call and clamps to non-negative.
    /// </summary>
    TimeSpan? RemainingPause { get; }
}
