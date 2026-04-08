using System.Collections.Concurrent;
using System.Diagnostics;
using Lfm.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Static helper for injecting performance observers, extracting timing metrics,
/// and tracking API calls via Playwright request/response events.
/// </summary>
public static class PerfHelper
{
    /// <summary>
    /// Injects a PerformanceObserver script into the page that captures LCP
    /// before navigation. Must be called before <see cref="IPage.GotoAsync"/>.
    /// </summary>
    public static async Task InjectPerformanceObserverAsync(IPage page)
    {
        await page.AddInitScriptAsync("""
            window.__lcpValue = null;
            const observer = new PerformanceObserver((list) => {
                const entries = list.getEntries();
                for (const entry of entries) {
                    window.__lcpValue = entry.startTime;
                }
            });
            observer.observe({ type: 'largest-contentful-paint', buffered: true });
            """);
    }

    /// <summary>
    /// Extracts page timing metrics after navigation completes.
    /// Returns TTFB, DOMContentLoaded, Load (all ms), and LCP if available.
    /// </summary>
    public static async Task<PerfMetrics> ExtractTimingMetricsAsync(
        IPage page,
        IReadOnlyList<ApiCallRecord>? apiCalls = null)
    {
        var timing = await page.EvaluateAsync<TimingResult>("""
            () => {
                const nav = performance.getEntriesByType('navigation')[0];
                const lcp = window.__lcpValue ?? null;
                return {
                    ttfb: nav ? nav.responseStart : -1,
                    domContentLoaded: nav ? nav.domContentLoadedEventEnd : -1,
                    load: nav ? nav.loadEventEnd : -1,
                    lcp: lcp
                };
            }
            """);

        return new PerfMetrics(
            Lcp: timing.Lcp,
            Ttfb: timing.Ttfb >= 0 ? timing.Ttfb : null,
            DomContentLoaded: timing.DomContentLoaded >= 0 ? timing.DomContentLoaded : null,
            Load: timing.Load >= 0 ? timing.Load : null,
            ApiCalls: apiCalls);
    }

    /// <summary>
    /// Attaches request/response event handlers to the page to track API calls.
    /// Returns a tracker object; pass it to <see cref="StopApiTracking"/> when done.
    /// </summary>
    public static ApiTracker StartApiTracking(IPage page)
    {
        var tracker = new ApiTracker(page);
        tracker.Attach();
        return tracker;
    }

    /// <summary>
    /// Detaches handlers from the tracker and returns the collected API call records.
    /// </summary>
    public static IReadOnlyList<ApiCallRecord> StopApiTracking(ApiTracker tracker)
    {
        tracker.Detach();
        return tracker.Records;
    }

    /// <summary>
    /// Formats performance metrics for <see cref="ITestOutputHelper"/> output.
    /// </summary>
    public static string FormatPerfOutput(string testName, PerfMetrics metrics)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[PERF] {testName}");
        sb.AppendLine($"  LCP:               {FormatMs(metrics.Lcp)}");
        sb.AppendLine($"  TTFB:              {FormatMs(metrics.Ttfb)}");
        sb.AppendLine($"  DOMContentLoaded:  {FormatMs(metrics.DomContentLoaded)}");
        sb.AppendLine($"  Load:              {FormatMs(metrics.Load)}");

        if (metrics.NavigationTime.HasValue)
        {
            sb.AppendLine($"  Navigation:        {FormatMs(metrics.NavigationTime)}");
        }

        if (metrics.ApiCalls is { Count: > 0 })
        {
            sb.AppendLine("  API Calls:");
            foreach (var call in metrics.ApiCalls)
            {
                var path = ExtractPath(call.Url);
                var size = call.Size.HasValue
                    ? $", {call.Size.Value / 1024.0:F1} KB"
                    : string.Empty;
                sb.AppendLine(
                    $"    {call.Method,-6} {path,-30} → {call.Status} ({call.Duration:F0} ms{size})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatMs(double? value)
        => value.HasValue ? $"{value.Value:N0} ms" : "n/a";

    private static string ExtractPath(string url)
    {
        try
        {
            return new Uri(url).PathAndQuery;
        }
        catch
        {
            return url;
        }
    }

    private sealed record TimingResult(
        double Ttfb,
        double DomContentLoaded,
        double Load,
        double? Lcp);
}

/// <summary>
/// Tracks API calls via Playwright request/response events.
/// Created by <see cref="PerfHelper.StartApiTracking"/>; stopped by
/// <see cref="PerfHelper.StopApiTracking"/>.
/// </summary>
public sealed class ApiTracker
{
    private readonly IPage _page;
    private readonly ConcurrentDictionary<string, (string Method, long StartTick)> _pending = new();
    private readonly ConcurrentBag<ApiCallRecord> _records = [];

    private EventHandler<IRequest>? _requestHandler;
    private EventHandler<IResponse>? _responseHandler;

    internal ApiTracker(IPage page) => _page = page;

    public IReadOnlyList<ApiCallRecord> Records => [.. _records];

    internal void Attach()
    {
        _requestHandler = (_, req) =>
        {
            if (req.Url.Contains("/api/", StringComparison.OrdinalIgnoreCase))
            {
                _pending[req.Url] = (req.Method, Stopwatch.GetTimestamp());
            }
        };

        _responseHandler = (_, resp) =>
        {
            if (!resp.Url.Contains("/api/", StringComparison.OrdinalIgnoreCase)) return;
            if (!_pending.TryRemove(resp.Url, out var entry)) return;

            var elapsed = Stopwatch.GetElapsedTime(entry.StartTick).TotalMilliseconds;

            long? size = null;
            try
            {
                var headers = resp.Headers;
                if (headers.TryGetValue("content-length", out var lenStr)
                    && long.TryParse(lenStr, out var len))
                {
                    size = len;
                }
            }
            catch
            {
                // Header may not be present.
            }

            _records.Add(new ApiCallRecord(
                Method: entry.Method,
                Url: resp.Url,
                Status: resp.Status,
                Duration: elapsed,
                Size: size));
        };

        _page.Request += _requestHandler;
        _page.Response += _responseHandler;
    }

    internal void Detach()
    {
        if (_requestHandler is not null) _page.Request -= _requestHandler;
        if (_responseHandler is not null) _page.Response -= _responseHandler;
    }
}
