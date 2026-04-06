using FluentAssertions;
using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Timing budgets mirroring frontend/e2e/perf/helpers/flowBudgets.ts.
/// </summary>
public static class AckBudget
{
    /// <summary>Standard interactions: button clicks, selections.</summary>
    public const int Standard = 200;

    /// <summary>Heavier transitions: route changes, panel swaps.</summary>
    public const int Heavy = 300;

    /// <summary>Cold entry loads: initial app shell render after navigation commit.</summary>
    public const int Entry = 500;
}

/// <summary>
/// Flow completion budgets mirroring frontend/e2e/perf/helpers/flowBudgets.ts.
/// </summary>
public static class CompletionBudget
{
    /// <summary>Fast transitions with local/cached data.</summary>
    public const int Fast = 1_000;

    /// <summary>Network-backed updates with visible loading.</summary>
    public const int Network = 2_000;

    /// <summary>Full-page redirects that re-enter the app after server-side auth.</summary>
    public const int Redirect = 2_500;

    /// <summary>Slower flows under mobile emulation.</summary>
    public const int Mobile = 3_000;
}

/// <summary>Layout stability thresholds.</summary>
public static class StabilityThreshold
{
    /// <summary>Maximum cumulative layout shift during an interaction window.</summary>
    public const double MaxCls = 0.1;

    /// <summary>Maximum single layout shift value.</summary>
    public const double MaxSingleShift = 0.05;
}

/// <summary>Result of a measured interaction.</summary>
public sealed record InteractionResult(
    double AckMs,
    double CompletionMs,
    StabilityData Stability);

/// <summary>Layout-shift and long-task data collected from the browser.</summary>
public sealed record StabilityData(
    IReadOnlyList<LayoutShiftEntry> LayoutShifts,
    double CumulativeLayoutShift);

/// <summary>A single layout-shift entry from the browser PerformanceObserver.</summary>
public sealed record LayoutShiftEntry(double Value, double StartTime);

/// <summary>
/// Playwright timing measurement helpers, mirroring frontend/e2e/perf/helpers/perfAssertions.ts.
/// Uses Stopwatch for wall-clock timing instead of browser performance.now() —
/// the overhead is negligible relative to the budgets.
/// </summary>
public static class PerfHelper
{
    private static readonly string InstallObserversScript = """
        () => {
            const win = window;
            win.__perfShifts = [];
            win.__perfLongTasks = [];

            new PerformanceObserver((list) => {
                for (const entry of list.getEntries()) {
                    if (!entry.hadRecentInput) {
                        win.__perfShifts.push({ value: entry.value, startTime: entry.startTime });
                    }
                }
            }).observe({ type: 'layout-shift', buffered: false });

            try {
                new PerformanceObserver((list) => {
                    for (const entry of list.getEntries()) {
                        win.__perfLongTasks.push({ duration: entry.duration, startTime: entry.startTime });
                    }
                }).observe({ type: 'longtask', buffered: false });
            } catch {}
        }
        """;

    private static readonly string CollectStabilityScript = """
        () => {
            const shifts = window.__perfShifts ?? [];
            const cls = shifts.reduce((sum, s) => sum + s.value, 0);
            return { layoutShifts: shifts, cumulativeLayoutShift: cls };
        }
        """;

    /// <summary>
    /// Installs browser-side PerformanceObservers. Call before the measured interaction.
    /// </summary>
    public static Task InstallObserversAsync(IPage page) =>
        page.EvaluateAsync(InstallObserversScript);

    /// <summary>
    /// Measures a single user interaction's acknowledgement and completion timing.
    ///
    /// 1. Installs browser observers
    /// 2. Records start time
    /// 3. Executes the action
    /// 4. Waits for ackMarker to be visible (acknowledgement)
    /// 5. Waits for completionMarker to be visible (completion)
    /// 6. Collects stability data
    /// </summary>
    public static async Task<InteractionResult> MeasureInteractionAsync(
        IPage page,
        Func<Task> action,
        ILocator ackMarker,
        ILocator completionMarker,
        int timeoutMs = 5_000)
    {
        await InstallObserversAsync(page);

        var start = DateTimeOffset.UtcNow;

        await action();

        await ackMarker.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        var ackMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;

        var remaining = Math.Max(timeoutMs - ackMs, 1_000);
        await completionMarker.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = (float)remaining });
        var completionMs = (DateTimeOffset.UtcNow - start).TotalMilliseconds;

        var stability = await CollectStabilityAsync(page);
        return new InteractionResult(ackMs, completionMs, stability);
    }

    private static async Task<StabilityData> CollectStabilityAsync(IPage page)
    {
        var raw = await page.EvaluateAsync<StabilityRaw>(CollectStabilityScript);
        return new StabilityData(
            raw.LayoutShifts.Select(s => new LayoutShiftEntry(s.Value, s.StartTime)).ToList(),
            raw.CumulativeLayoutShift);
    }

    /// <summary>Assert acknowledgement happened within budget.</summary>
    public static void AssertAcknowledgementWithin(InteractionResult result, int budgetMs)
    {
        result.AckMs.Should().BeLessThanOrEqualTo(
            budgetMs,
            $"acknowledgement took {result.AckMs:F0}ms, budget is {budgetMs}ms");
    }

    /// <summary>Assert completion happened within budget.</summary>
    public static void AssertCompletionWithin(InteractionResult result, int budgetMs)
    {
        result.CompletionMs.Should().BeLessThanOrEqualTo(
            budgetMs,
            $"completion took {result.CompletionMs:F0}ms, budget is {budgetMs}ms");
    }

    /// <summary>Assert no major layout instability during the interaction window.</summary>
    public static void AssertStableInteraction(InteractionResult result)
    {
        result.Stability.CumulativeLayoutShift.Should().BeLessThanOrEqualTo(
            StabilityThreshold.MaxCls,
            $"CLS {result.Stability.CumulativeLayoutShift:F4} exceeds max {StabilityThreshold.MaxCls}");

        foreach (var shift in result.Stability.LayoutShifts)
        {
            shift.Value.Should().BeLessThanOrEqualTo(
                StabilityThreshold.MaxSingleShift,
                $"single layout shift {shift.Value:F4} exceeds max {StabilityThreshold.MaxSingleShift}");
        }
    }

    // JSON deserialization target — field names match the browser script output.
    private sealed record StabilityRaw(
        ShiftRaw[] LayoutShifts,
        double CumulativeLayoutShift);

    private sealed record ShiftRaw(double Value, double StartTime);
}
