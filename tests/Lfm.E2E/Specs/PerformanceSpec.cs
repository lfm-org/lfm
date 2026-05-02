// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Runs")]
[Trait("Category", E2ELanes.Performance)]
public class PerformanceSpec(RunsFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    private readonly List<PerformanceJourneyResult> _results = [];
    private readonly ConcurrentQueue<string> _requestFailures = new();

    public override Task InitializeAsync() => base.InitializeAsync();

    public override async Task DisposeAsync()
    {
        WriteReport();
        await base.DisposeAsync();
    }

    [Fact]
    [Trait("Category", E2ELanes.Performance)]
    public async Task Stable_journeys_emit_timing_report()
    {
        await MeasureColdPublicLandingAsync();
        await MeasureAuthenticatedRoutesAsync();

        Assert.All(_results, result =>
            Assert.True(
                result.ElapsedMs <= result.BudgetMs,
                $"{result.Name} took {result.ElapsedMs} ms, above loose budget {result.BudgetMs} ms"));
    }

    private async Task MeasureColdPublicLandingAsync()
    {
        var context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        var page = await context.NewPageAsync();
        try
        {
            WatchRequestFailures(page);

            await MeasureAsync(
                "cold-public-landing",
                TimeSpan.FromSeconds(60),
                page,
                async () =>
                {
                    await page.GotoAsync(fixture.Stack.AppBaseUrl + "/", new() { WaitUntil = WaitUntilState.NetworkIdle });
                    await Assertions.Expect(new LandingPage(page).Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
                });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task MeasureAuthenticatedRoutesAsync()
    {
        var context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var page = await context.NewPageAsync();
        try
        {
            WatchRequestFailures(page);

            await page.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "{\"portraits\":{}}",
                });
            });

            var runsPage = new RunsPage(page);
            await MeasureAsync(
                "authenticated-runs-load",
                TimeSpan.FromSeconds(60),
                page,
                async () =>
                {
                    await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });
                    await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId)).ToBeVisibleAsync(new() { Timeout = 15000 });
                });

            await MeasureAsync(
                "run-create-form-load",
                TimeSpan.FromSeconds(45),
                page,
                async () =>
                {
                    await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(runsPage.KeyLevelInput).ToBeVisibleAsync(new() { Timeout = 15000 });
                });

            await MeasureAsync(
                "characters-list-load",
                TimeSpan.FromSeconds(45),
                page,
                async () =>
                {
                    var charactersPage = new CharactersPage(page);
                    await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
                });

            await page.GotoAsync(fixture.Stack.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.NetworkIdle });
            await MeasureAsync(
                "warm-route-navigation",
                TimeSpan.FromSeconds(20),
                page,
                async () =>
                {
                    await new NavBar(page).CharactersLink.ClickAsync();
                    await Assertions.Expect(new CharactersPage(page).Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
                });
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task MeasureAsync(
        string name,
        TimeSpan budget,
        IPage page,
        Func<Task> action)
    {
        var beforeFailures = _requestFailures.Count;
        var sw = Stopwatch.StartNew();
        await action();
        sw.Stop();

        var navigationDurationMs = await ReadNavigationDurationMsAsync(page);
        var failedRequests = _requestFailures.Skip(beforeFailures).ToArray();
        _results.Add(new PerformanceJourneyResult(
            name,
            (long)sw.Elapsed.TotalMilliseconds,
            (long)budget.TotalMilliseconds,
            navigationDurationMs,
            failedRequests));
    }

    private void WatchRequestFailures(IPage page)
    {
        page.RequestFailed += (_, request) =>
        {
            _requestFailures.Enqueue($"{request.Method} {request.Url} {request.Failure}");
        };
    }

    private static async Task<double?> ReadNavigationDurationMsAsync(IPage page)
    {
        try
        {
            return await page.EvaluateAsync<double?>(
                """
                () => {
                  const nav = performance.getEntriesByType("navigation").at(-1);
                  return nav ? Math.round(nav.duration) : null;
                }
                """);
        }
        catch
        {
            return null;
        }
    }

    private void WriteReport()
    {
        var repoRoot = FindRepoRoot();
        var outputDir = Path.Combine(repoRoot, "artifacts", "e2e-results");
        Directory.CreateDirectory(outputDir);

        var report = new PerformanceReport(
            DateTimeOffset.UtcNow,
            "Loose local-stack regression guards; production truth comes from RUM and telemetry.",
            _results);

        var path = Path.Combine(outputDir, "performance-report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Log($"[ARTIFACT] Performance report: {path}");
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

    private sealed record PerformanceReport(
        DateTimeOffset GeneratedAt,
        string BudgetPolicy,
        IReadOnlyList<PerformanceJourneyResult> Journeys);

    private sealed record PerformanceJourneyResult(
        string Name,
        long ElapsedMs,
        long BudgetMs,
        double? NavigationDurationMs,
        IReadOnlyList<string> RequestFailures);
}
