// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using System.Net.Http.Headers;

namespace Lfm.Api.Services;

/// <summary>
/// DelegatingHandler that gates every outbound Blizzard request on the shared
/// <see cref="IBlizzardRateLimiter"/>. On an upstream 429, extracts Retry-After
/// and pauses the shared limiter so concurrent callers back off together.
/// </summary>
public sealed class BlizzardRateLimitHandler(IBlizzardRateLimiter limiter) : DelegatingHandler
{
    // Upper bound on the Retry-After we honour from upstream. A buggy or hostile
    // 429 with Retry-After: 999999 would otherwise pause the shared limiter (and
    // therefore every authenticated user's outbound traffic) for ~11 days. Real
    // Blizzard throttle windows are seconds, not minutes; 60s is conservative.
    internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(60);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        using var lease = await limiter.AcquireAsync(ct);
        if (!lease.IsAcquired)
        {
            var synthetic = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            synthetic.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return synthetic;
        }

        var response = await base.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                ?? TimeSpan.FromSeconds(1);
            if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.FromSeconds(1);
            if (retryAfter > MaxRetryAfter) retryAfter = MaxRetryAfter;
            limiter.PauseUntil(DateTimeOffset.UtcNow + retryAfter);
        }
        return response;
    }
}
