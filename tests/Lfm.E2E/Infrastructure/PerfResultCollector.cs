using System.Collections.Concurrent;

namespace Lfm.E2E.Infrastructure;

/// <summary>
/// Static thread-safe collector for performance metrics. Integrated into
/// <see cref="TestResultCollector"/>'s JSON output under the "performance" key.
/// </summary>
public static class PerfResultCollector
{
    private static readonly ConcurrentDictionary<string, PerfMetrics> Metrics = new();

    /// <summary>
    /// Records performance metrics for a named test. Thread-safe.
    /// </summary>
    public static void Record(string testName, PerfMetrics metrics)
    {
        Metrics[testName] = metrics;
    }

    /// <summary>
    /// Returns all collected metrics as a read-only dictionary.
    /// Returns null when no metrics have been recorded.
    /// </summary>
    public static IReadOnlyDictionary<string, PerfMetrics>? GetAll()
    {
        return Metrics.IsEmpty ? null : Metrics;
    }
}

/// <summary>
/// Performance metrics captured for a single page or navigation.
/// All timing values are in milliseconds.
/// </summary>
public sealed record PerfMetrics(
    double? Lcp = null,
    double? Ttfb = null,
    double? DomContentLoaded = null,
    double? Load = null,
    double? NavigationTime = null,
    IReadOnlyList<ApiCallRecord>? ApiCalls = null);

/// <summary>
/// A single API call captured via Playwright request/response events.
/// </summary>
public sealed record ApiCallRecord(
    string Method,
    string Url,
    int Status,
    double Duration,
    long? Size = null);
