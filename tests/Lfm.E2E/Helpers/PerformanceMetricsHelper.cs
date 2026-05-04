// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

internal static class PerformanceMetricsHelper
{
    public const string MetricSource =
        "Chromium lab PerformanceObserver/resource timing captured by Playwright against the local E2E stack.";

    public const string ThresholdSource =
        "web.dev Core Web Vitals poor thresholds: LCP > 4000 ms, CLS > 0.25, INP poor proxy > 500 ms at p75.";

    public const string GatePolicy =
        "Hybrid gate: request/browser failures and local p75 poor-threshold regressions fail; good thresholds and timing drift stay advisory.";

    private const long LargestContentfulPaintPoorThresholdMs = 4000;
    private const double CumulativeLayoutShiftPoorThreshold = 0.25;
    private const long ControlledInteractionPoorThresholdMs = 500;

    public static async Task StartCollectionAsync(IPage page)
    {
        await page.AddInitScriptAsync(ObserverScript);
        await page.EvaluateAsync(ObserverScript);
        await page.EvaluateAsync(
            """
            () => {
              window.__lfmPerf?.reset?.();
              performance.clearResourceTimings?.();
            }
            """);
    }

    public static async Task<BrowserPerformanceMetrics> ReadAsync(
        IPage page,
        double? controlledInteractionDurationMs)
    {
        var snapshot = await page.EvaluateAsync<BrowserPerformanceSnapshot>(
            """
            () => {
              const perf = window.__lfmPerf;
              return {
                supportsLargestContentfulPaint: !!perf?.supportsLargestContentfulPaint,
                supportsLayoutShift: !!perf?.supportsLayoutShift,
                supportsEventTiming: !!perf?.supportsEventTiming,
                largestContentfulPaintMs: perf?.largestContentfulPaintMs ?? null,
                cumulativeLayoutShift: perf?.cumulativeLayoutShift ?? 0
              };
            }
            """);

        var resources = await page.EvaluateAsync<ResourceTimingMetric[]>(
            """
            () => performance.getEntriesByType("resource").map(entry => ({
              name: entry.name,
              initiatorType: entry.initiatorType || "",
              durationMs: Math.round(entry.duration),
              transferSize: Math.max(0, Math.round(entry.transferSize || 0))
            }))
            """);

        var apiResources = resources
            .Where(resource => Uri.TryCreate(resource.Name, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal))
            .ToArray();

        return new BrowserPerformanceMetrics(
            snapshot.SupportsLargestContentfulPaint,
            snapshot.SupportsLayoutShift,
            snapshot.SupportsEventTiming,
            RoundToLong(snapshot.LargestContentfulPaintMs),
            Math.Round(snapshot.CumulativeLayoutShift, 4),
            RoundToLong(controlledInteractionDurationMs),
            resources.Length,
            apiResources.Length,
            apiResources.Sum(resource => resource.TransferSize),
            PercentileOrNull(apiResources.Select(resource => resource.DurationMs).ToArray(), 75),
            MaxOrNull(apiResources.Select(resource => (long)resource.DurationMs).ToArray()));
    }

    public static BrowserPerformanceMetricSummary Summarize(
        IReadOnlyCollection<BrowserPerformanceMetrics> samples)
    {
        var lcp = samples
            .Where(sample => sample.SupportsLargestContentfulPaint)
            .Select(sample => sample.LargestContentfulPaintMs)
            .OfType<long>()
            .ToArray();
        var cls = samples
            .Where(sample => sample.SupportsLayoutShift)
            .Select(sample => sample.CumulativeLayoutShift)
            .ToArray();
        var interactions = samples
            .Select(sample => sample.ControlledInteractionDurationMs)
            .OfType<long>()
            .ToArray();
        var requestCounts = samples.Select(sample => (long)sample.ApiRequestCount).ToArray();
        var transferBytes = samples.Select(sample => sample.ApiTransferBytes).ToArray();
        var apiDurations = samples
            .Select(sample => sample.P75ApiDurationMs)
            .OfType<long>()
            .ToArray();

        return new BrowserPerformanceMetricSummary(
            samples.Count,
            samples.Any(sample => sample.SupportsLargestContentfulPaint),
            samples.Any(sample => sample.SupportsLayoutShift),
            samples.Any(sample => sample.SupportsEventTiming),
            PercentileOrNull(lcp, 50),
            PercentileOrNull(lcp, 75),
            MaxOrNull(lcp),
            PercentileOrNull(cls, 50),
            PercentileOrNull(cls, 75),
            MaxOrNull(cls),
            PercentileOrNull(interactions, 50),
            PercentileOrNull(interactions, 75),
            MaxOrNull(interactions),
            PercentileOrNull(requestCounts, 50),
            PercentileOrNull(requestCounts, 75),
            MaxOrNull(requestCounts),
            PercentileOrNull(transferBytes, 50),
            PercentileOrNull(transferBytes, 75),
            MaxOrNull(transferBytes),
            PercentileOrNull(apiDurations, 50),
            PercentileOrNull(apiDurations, 75),
            MaxOrNull(apiDurations));
    }

    public static IReadOnlyList<string> EvaluatePoorThresholds(
        string journey,
        string viewport,
        BrowserPerformanceMetricSummary summary)
    {
        var failures = new List<string>();

        if (summary.P75LargestContentfulPaintMs > LargestContentfulPaintPoorThresholdMs)
        {
            failures.Add(
                $"{journey} [{viewport}] LCP p75 {summary.P75LargestContentfulPaintMs.Value.ToString(CultureInfo.InvariantCulture)} ms " +
                $"exceeds local poor threshold {LargestContentfulPaintPoorThresholdMs} ms");
        }

        if (summary.P75CumulativeLayoutShift > CumulativeLayoutShiftPoorThreshold)
        {
            failures.Add(
                $"{journey} [{viewport}] CLS p75 {summary.P75CumulativeLayoutShift.Value.ToString("0.####", CultureInfo.InvariantCulture)} " +
                $"exceeds local poor threshold {CumulativeLayoutShiftPoorThreshold.ToString("0.##", CultureInfo.InvariantCulture)}");
        }

        if (summary.P75ControlledInteractionDurationMs > ControlledInteractionPoorThresholdMs)
        {
            failures.Add(
                $"{journey} [{viewport}] controlled interaction p75 {summary.P75ControlledInteractionDurationMs.Value.ToString(CultureInfo.InvariantCulture)} ms " +
                $"exceeds local poor threshold {ControlledInteractionPoorThresholdMs} ms");
        }

        return failures;
    }

    public static long Percentile(IReadOnlyList<long> values, int percentile)
    {
        if (values.Count == 0)
            return 0;

        return RoundToLong(Percentile(values.Select(value => (double)value).ToArray(), percentile)) ?? 0;
    }

    private static long? PercentileOrNull(IReadOnlyList<long> values, int percentile) =>
        values.Count == 0 ? null : Percentile(values, percentile);

    private static long? PercentileOrNull(IReadOnlyList<int> values, int percentile) =>
        values.Count == 0 ? null : Percentile(values.Select(value => (long)value).ToArray(), percentile);

    private static double? PercentileOrNull(IReadOnlyList<double> values, int percentile) =>
        values.Count == 0 ? null : Math.Round(Percentile(values, percentile), 4);

    private static double Percentile(IReadOnlyList<double> values, int percentile)
    {
        var sorted = values.OrderBy(value => value).ToArray();
        var rank = (percentile / 100d) * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper)
            return sorted[lower];

        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (rank - lower));
    }

    private static long? MaxOrNull(IReadOnlyList<long> values) =>
        values.Count == 0 ? null : values.Max();

    private static double? MaxOrNull(IReadOnlyList<double> values) =>
        values.Count == 0 ? null : Math.Round(values.Max(), 4);

    private static long? RoundToLong(double? value) =>
        value is null ? null : (long)Math.Round(value.Value);

    private sealed class BrowserPerformanceSnapshot
    {
        public bool SupportsLargestContentfulPaint { get; set; }
        public bool SupportsLayoutShift { get; set; }
        public bool SupportsEventTiming { get; set; }
        public double? LargestContentfulPaintMs { get; set; }
        public double CumulativeLayoutShift { get; set; }
    }

    private sealed class ResourceTimingMetric
    {
        public string Name { get; set; } = string.Empty;
        public string InitiatorType { get; set; } = string.Empty;
        public int DurationMs { get; set; }
        public long TransferSize { get; set; }
    }

    private const string ObserverScript =
        """
        (() => {
          if (window.__lfmPerf?.version === 1) return;

          const supported = PerformanceObserver.supportedEntryTypes || [];
          const state = {
            version: 1,
            supportsLargestContentfulPaint: supported.includes("largest-contentful-paint"),
            supportsLayoutShift: supported.includes("layout-shift"),
            supportsEventTiming: supported.includes("event"),
            largestContentfulPaintMs: null,
            cumulativeLayoutShift: 0,
            reset() {
              this.largestContentfulPaintMs = null;
              this.cumulativeLayoutShift = 0;
            }
          };

          const observe = (type, callback) => {
            try {
              new PerformanceObserver(list => {
                for (const entry of list.getEntries()) {
                  callback(entry);
                }
              }).observe({ type, buffered: true });
            } catch {
              // Unsupported metrics stay marked through the support flags.
            }
          };

          if (state.supportsLargestContentfulPaint) {
            observe("largest-contentful-paint", entry => {
              state.largestContentfulPaintMs =
                Math.round(entry.renderTime || entry.loadTime || entry.startTime || 0);
            });
          }

          if (state.supportsLayoutShift) {
            observe("layout-shift", entry => {
              if (!entry.hadRecentInput) {
                state.cumulativeLayoutShift += entry.value || 0;
              }
            });
          }

          window.__lfmPerf = state;
        })();
        """;
}

internal sealed record BrowserPerformanceMetrics(
    bool SupportsLargestContentfulPaint,
    bool SupportsLayoutShift,
    bool SupportsEventTiming,
    long? LargestContentfulPaintMs,
    double CumulativeLayoutShift,
    long? ControlledInteractionDurationMs,
    int ResourceCount,
    int ApiRequestCount,
    long ApiTransferBytes,
    long? P75ApiDurationMs,
    long? MaxApiDurationMs);

internal sealed record BrowserPerformanceMetricSummary(
    int SampleCount,
    bool SupportsLargestContentfulPaint,
    bool SupportsLayoutShift,
    bool SupportsEventTiming,
    long? P50LargestContentfulPaintMs,
    long? P75LargestContentfulPaintMs,
    long? MaxLargestContentfulPaintMs,
    double? P50CumulativeLayoutShift,
    double? P75CumulativeLayoutShift,
    double? MaxCumulativeLayoutShift,
    long? P50ControlledInteractionDurationMs,
    long? P75ControlledInteractionDurationMs,
    long? MaxControlledInteractionDurationMs,
    long? P50ApiRequestCount,
    long? P75ApiRequestCount,
    long? MaxApiRequestCount,
    long? P50ApiTransferBytes,
    long? P75ApiTransferBytes,
    long? MaxApiTransferBytes,
    long? P50ApiDurationMs,
    long? P75ApiDurationMs,
    long? MaxApiDurationMs);
