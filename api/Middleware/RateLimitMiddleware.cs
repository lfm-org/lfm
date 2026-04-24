// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.RateLimiting;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Middleware;

/// <summary>
/// Per-IP rate limiting for auth, write, and read endpoints using sliding
/// window limiters. OPTIONS bypasses entirely (handled by CorsMiddleware).
/// </summary>
public sealed class RateLimitMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly HashSet<string> AuthFunctions =
        new(["battlenet-login", "battlenet-callback"], StringComparer.OrdinalIgnoreCase);

    // Endpoints that should sit on a stricter tier than ordinary reads.
    // privacy-email is the address-reveal probe behind the privacy page's
    // click-to-reveal button — a single browser session legitimately calls it
    // at most a handful of times per minute, so 5/min is headroom for real
    // users and a brick wall for scrapers.
    private static readonly HashSet<string> PrivacyFunctions =
        new(["privacy-email"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WriteMethods =
        new(["POST", "PUT", "PATCH", "DELETE"], StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _authLimiters = new();
    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _writeLimiters = new();
    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _readLimiters = new();
    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _privacyLimiters = new();

    private long _callCount;
    private readonly RateLimitOptions _options;
    private readonly HashSet<string> _trustedProxies;

    public RateLimitMiddleware(IOptions<RateLimitOptions> opts)
    {
        _options = opts.Value;
        _trustedProxies = new HashSet<string>(_options.TrustedProxyAddresses, StringComparer.OrdinalIgnoreCase);
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        if (!_options.Enabled)
        {
            await next(context);
            return;
        }

        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var method = httpContext.Request.Method;

        // OPTIONS bypasses — preflight is handled by CorsMiddleware.
        if (string.Equals(method, HttpMethods.Options, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var functionName = context.FunctionDefinition.Name;
        var isAuth = AuthFunctions.Contains(functionName);
        var isPrivacy = !isAuth && PrivacyFunctions.Contains(functionName);
        var isWrite = !isAuth && !isPrivacy && WriteMethods.Contains(method);
        var isRead = !isAuth && !isPrivacy && !isWrite && string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase);

        if (!isAuth && !isPrivacy && !isWrite && !isRead)
        {
            await next(context);
            return;
        }

        // Evict stale entries periodically
        var count = Interlocked.Increment(ref _callCount);
        if (count % 500 == 0)
        {
            EvictStaleEntries(_authLimiters);
            EvictStaleEntries(_writeLimiters);
            EvictStaleEntries(_readLimiters);
            EvictStaleEntries(_privacyLimiters);
        }

        var clientIp = GetClientIp(httpContext);
        var (limiters, permitLimit) = (isAuth, isPrivacy, isWrite) switch
        {
            (true, _, _) => (_authLimiters, _options.AuthRequestsPerMinute),
            (_, true, _) => (_privacyLimiters, _options.PrivacyRequestsPerMinute),
            (_, _, true) => (_writeLimiters, _options.WriteRequestsPerMinute),
            _ => (_readLimiters, _options.ReadRequestsPerMinute),
        };

        var entry = limiters.AddOrUpdate(
            clientIp,
            _ => (CreateLimiter(permitLimit), DateTimeOffset.UtcNow),
            (_, existing) => (existing.Limiter, DateTimeOffset.UtcNow));

        using var lease = entry.Limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.Headers["Retry-After"] = "60";
            httpContext.Response.ContentType = "application/json";
            await httpContext.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Too many requests" }));
            return;
        }

        await next(context);
    }

    internal string GetClientIp(HttpContext httpContext)
    {
        var remote = httpContext.Connection.RemoteIpAddress?.ToString();

        // Only honour X-Forwarded-For when the TCP peer is a known, configured
        // proxy. Otherwise a direct attacker can forge the header and spin up
        // arbitrarily many fresh buckets, bypassing rate limits entirely.
        if (remote is not null && _trustedProxies.Contains(remote))
        {
            var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwarded))
            {
                // Take the rightmost entry: the proxy that added it is trusted,
                // and any entries to its left could have been forged by the
                // original client. The rightmost is the only hop the trusted
                // proxy actually witnessed.
                var parts = forwarded.Split(',', StringSplitOptions.TrimEntries);
                var lastIp = parts[^1];
                if (!string.IsNullOrEmpty(lastIp))
                {
                    return lastIp;
                }
            }
        }

        return remote ?? "unknown";
    }

    private static SlidingWindowRateLimiter CreateLimiter(int permitLimit)
    {
        return new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            AutoReplenishment = true,
        });
    }

    private static void EvictStaleEntries(
        ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> limiters)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (var kvp in limiters)
        {
            if (kvp.Value.LastAccessed < cutoff)
            {
                if (limiters.TryRemove(kvp.Key, out var removed))
                {
                    removed.Limiter.Dispose();
                }
            }
        }
    }
}
