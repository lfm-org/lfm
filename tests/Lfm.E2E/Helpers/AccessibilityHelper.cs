using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Wraps Deque axe-core WCAG 2.2 AA scans with structured violation output.
/// </summary>
public static class AccessibilityHelper
{
    private static readonly AxeRunOptions WcagOptions = new()
    {
        RunOnly = new RunOnlyOptions
        {
            Type = "tag",
            Values = ["wcag2a", "wcag2aa", "wcag22aa"],
        },
    };

    /// <summary>
    /// Runs an axe-core scan on the current page with WCAG 2.2 AA tags and
    /// asserts there are zero violations. Violation details are written to
    /// <paramref name="output"/> for easy debugging in test output.
    /// </summary>
    /// <param name="page">The Playwright page to scan.</param>
    /// <param name="output">xUnit test output helper.</param>
    /// <param name="context">Optional human-readable label included in the assertion message.</param>
    public static async Task ScanAndAssert(IPage page, ITestOutputHelper output, string? context = null)
    {
        var results = await page.RunAxe(WcagOptions);

        foreach (var v in results.Violations)
        {
            output.WriteLine($"[AXE VIOLATION] {v.Id} ({v.Impact})");
            output.WriteLine($"  Rule: {v.Description}");
            output.WriteLine($"  WCAG: {string.Join(", ", v.Tags)}");
            foreach (var node in v.Nodes)
            {
                output.WriteLine($"  Target: {string.Join(" > ", node.Target)}");
                output.WriteLine($"  HTML: {node.Html}");
                var fixes = node.Any.Concat(node.All).Concat(node.None)
                    .Select(c => c.Message)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct();
                foreach (var fix in fixes)
                {
                    output.WriteLine($"  Fix: {fix}");
                }
            }
        }

        results.Violations.Should().BeEmpty(
            $"page {context ?? page.Url} has WCAG 2.2 AA violations");
    }
}
