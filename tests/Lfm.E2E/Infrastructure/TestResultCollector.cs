// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lfm.E2E.Infrastructure;

/// <summary>
/// Static thread-safe collector that accumulates test results across all
/// collections and writes a JSON report on process exit.
/// </summary>
public static class TestResultCollector
{
    private static readonly ConcurrentBag<TestResult> Results = [];
    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static bool _registered;
    private static readonly object Lock = new();

    /// <summary>
    /// Records a test result. Thread-safe.
    /// </summary>
    public static void Record(TestResult result)
    {
        EnsureRegistered();
        Results.Add(result);
    }

    private static void EnsureRegistered()
    {
        if (_registered) return;
        lock (Lock)
        {
            if (_registered) return;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            _registered = true;
        }
    }

    private static void Flush()
    {
        var elapsed = DateTime.UtcNow - StartTime;
        var results = Results.ToArray();

        var passed = results.Count(r => r.Status == TestStatus.Passed);
        var failed = results.Count(r => r.Status == TestStatus.Failed);
        var skipped = results.Count(r => r.Status == TestStatus.Skipped);

        // Write summary to stdout
        Console.WriteLine();
        Console.WriteLine($"[E2E SUMMARY] {passed} passed, {failed} failed, {skipped} skipped ({elapsed.TotalSeconds:F1}s)");

        if (failed > 0)
        {
            Console.WriteLine("  Failed:");
            foreach (var r in results.Where(r => r.Status == TestStatus.Failed))
            {
                Console.WriteLine($"    {r.Name} — {r.Error ?? "unknown error"}");
            }
        }

        // Write JSON report
        try
        {
            var repoRoot = FindRepoRoot();
            var dir = Path.Combine(repoRoot, "artifacts", "e2e-results");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "e2e-report.json");

            var report = new TestReport(
                Timestamp: DateTime.UtcNow.ToString("o"),
                Duration: $"{elapsed.TotalSeconds:F1}s",
                Summary: new ReportSummary(results.Length, passed, failed, skipped),
                Tests: results);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
            };

            File.WriteAllText(path, JsonSerializer.Serialize(report, options));
        }
        catch
        {
            // Best effort — do not mask test failures.
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "lfm.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find lfm.sln walking up from " + AppContext.BaseDirectory);
    }
}

// ----- Records -----

public enum TestStatus
{
    Passed,
    Failed,
    Skipped,
}

/// <summary>
/// An individual test result as recorded by the collector.
/// </summary>
public sealed record TestResult(
    string Name,
    string Collection,
    string Category,
    TestStatus Status,
    string Duration,
    string? Error = null,
    string? DomSnapshot = null,
    IReadOnlyList<AxeViolation>? Violations = null);

/// <summary>
/// A single axe-core WCAG violation for structured JSON output.
/// </summary>
public sealed record AxeViolation(
    string Rule,
    string Impact,
    IReadOnlyList<string> WcagTags,
    string Target,
    string Html,
    string Fix);

/// <summary>
/// Top-level report serialized to e2e-report.json.
/// </summary>
public sealed record TestReport(
    string Timestamp,
    string Duration,
    ReportSummary Summary,
    IReadOnlyList<TestResult> Tests);

/// <summary>
/// Summary counts for the report.
/// </summary>
public sealed record ReportSummary(
    int Total,
    int Passed,
    int Failed,
    int Skipped);
