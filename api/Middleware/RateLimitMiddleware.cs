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
/// Per-IP rate limiting for auth and write endpoints using sliding window limiters.
/// GET and OPTIONS requests are not rate limited.
/// </summary>
public sealed class RateLimitMiddleware(IOptions<RateLimitOptions> opts) : IFunctionsWorkerMiddleware
{
    private static readonly HashSet<string> AuthFunctions =
        new(["battlenet-login", "battlenet-callback"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WriteMethods =
        new(["POST", "PUT", "PATCH", "DELETE"], StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _authLimiters = new();
    private readonly ConcurrentDictionary<string, (SlidingWindowRateLimiter Limiter, DateTimeOffset LastAccessed)> _writeLimiters = new();

    private long _callCount;
    private readonly RateLimitOptions _options = opts.Value;

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

        // OPTIONS and GET are never rate limited
        if (string.Equals(method, HttpMethods.Options, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, HttpMethods.Get, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var functionName = context.FunctionDefinition.Name;
        var isAuth = AuthFunctions.Contains(functionName);
        var isWrite = !isAuth && WriteMethods.Contains(method);

        if (!isAuth && !isWrite)
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
        }

        var clientIp = GetClientIp(httpContext);
        var limiters = isAuth ? _authLimiters : _writeLimiters;
        var permitLimit = isAuth ? _options.AuthRequestsPerMinute : _options.WriteRequestsPerMinute;

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

    internal static string GetClientIp(HttpContext httpContext)
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwarded))
        {
            // Take the first entry (leftmost = original client)
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrEmpty(firstIp))
            {
                return firstIp;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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
