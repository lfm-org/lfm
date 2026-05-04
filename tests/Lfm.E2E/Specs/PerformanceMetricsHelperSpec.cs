// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Helpers;
using Xunit;

namespace Lfm.E2E.Specs;

[Trait("Category", "Performance policy")]
public class PerformanceMetricsHelperSpec
{
    [Fact]
    public void Percentile_InterpolatesP75ForSmallLocalSamples()
    {
        var p75 = PerformanceMetricsHelper.Percentile([100, 200, 400, 800], 75);

        Assert.Equal(500, p75);
    }

    [Fact]
    public void SummarizeBrowserMetrics_ReportsP75AndSupportFlags()
    {
        var samples = new[]
        {
            new BrowserPerformanceMetrics(true, true, true, 1000, 0.01, 100, 3, 2, 1024, 40, 80),
            new BrowserPerformanceMetrics(true, true, true, 2000, 0.02, 200, 4, 2, 2048, 80, 120),
            new BrowserPerformanceMetrics(true, true, true, 3000, 0.04, 400, 5, 3, 4096, 120, 200),
            new BrowserPerformanceMetrics(true, true, true, 5000, 0.08, 800, 6, 4, 8192, 160, 240),
        };

        var summary = PerformanceMetricsHelper.Summarize(samples);

        Assert.True(summary.SupportsLargestContentfulPaint);
        Assert.True(summary.SupportsLayoutShift);
        Assert.True(summary.SupportsEventTiming);
        Assert.Equal(3500, summary.P75LargestContentfulPaintMs);
        Assert.Equal(0.05, summary.P75CumulativeLayoutShift);
        Assert.Equal(500, summary.P75ControlledInteractionDurationMs);
        Assert.Equal(3, summary.P75ApiRequestCount);
        Assert.Equal(5120, summary.P75ApiTransferBytes);
        Assert.Equal(130, summary.P75ApiDurationMs);
    }

    [Fact]
    public void EvaluatePoorThresholds_FailsOnlySupportedMetricsAbovePoorThreshold()
    {
        var summary = new BrowserPerformanceMetricSummary(
            SampleCount: 4,
            SupportsLargestContentfulPaint: true,
            SupportsLayoutShift: true,
            SupportsEventTiming: true,
            P50LargestContentfulPaintMs: 3000,
            P75LargestContentfulPaintMs: 4500,
            MaxLargestContentfulPaintMs: 6000,
            P50CumulativeLayoutShift: 0.10,
            P75CumulativeLayoutShift: 0.20,
            MaxCumulativeLayoutShift: 0.30,
            P50ControlledInteractionDurationMs: 300,
            P75ControlledInteractionDurationMs: 600,
            MaxControlledInteractionDurationMs: 700,
            P50ApiRequestCount: 2,
            P75ApiRequestCount: 3,
            MaxApiRequestCount: 4,
            P50ApiTransferBytes: 2048,
            P75ApiTransferBytes: 3072,
            MaxApiTransferBytes: 4096,
            P50ApiDurationMs: 100,
            P75ApiDurationMs: 125,
            MaxApiDurationMs: 250);

        var failures = PerformanceMetricsHelper.EvaluatePoorThresholds(
            "warm-route-navigation",
            "mobile",
            summary);

        Assert.Contains(
            "warm-route-navigation [mobile] LCP p75 4500 ms exceeds local poor threshold 4000 ms",
            failures);
        Assert.Contains(
            "warm-route-navigation [mobile] controlled interaction p75 600 ms exceeds local poor threshold 500 ms",
            failures);
        Assert.DoesNotContain(failures, failure => failure.Contains("CLS", StringComparison.Ordinal));
    }
}
