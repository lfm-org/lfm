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
    private const int ReportSchemaVersion = 2;
    private const int DefaultSampleCount = 2;

    private static readonly PerformanceViewport[] Viewports =
    [
        new("desktop", 1366, 768),
        new("mobile", 390, 844),
    ];

    private readonly List<PerformanceJourneyResult> _results = [];
    private readonly int _sampleCount = ReadSampleCount();

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
        foreach (var viewport in Viewports)
        {
            await MeasureColdPublicLandingAsync(viewport);
            await MeasureExpiredSessionRedirectAsync(viewport);
            await MeasureAuthenticatedRoutesAsync(viewport);
        }

        Assert.All(_results, result =>
            Assert.True(
                result.MaxElapsedMs <= result.BudgetMs,
                $"{result.Name} [{result.Viewport.Name}] max {result.MaxElapsedMs} ms, " +
                $"above loose budget {result.BudgetMs} ms; samples: " +
                string.Join(", ", result.Samples.Select(sample => sample.ElapsedMs))));

        var diagnosticFailures = _results
            .SelectMany(result => result.Samples.SelectMany(sample =>
                sample.RequestFailures
                    .Concat(sample.HttpFailures)
                    .Concat(sample.ConsoleErrors)
                    .Select(failure =>
                        $"{result.Name} [{result.Viewport.Name}] sample {sample.Sample}: " +
                        $"{failure.Type}: {failure.Message}")))
            .ToArray();

        Assert.True(
            diagnosticFailures.Length == 0,
            "Performance journeys emitted unexpected browser/network failures:\n"
            + string.Join("\n", diagnosticFailures));

        var poorThresholdFailures = _results
            .SelectMany(result => PerformanceMetricsHelper.EvaluatePoorThresholds(
                result.Name,
                result.Viewport.Name,
                result.BrowserMetrics))
            .ToArray();

        Assert.True(
            poorThresholdFailures.Length == 0,
            "Performance journeys exceeded local browser poor-threshold gates:\n"
            + string.Join("\n", poorThresholdFailures));
    }

    private async Task MeasureColdPublicLandingAsync(PerformanceViewport viewport)
    {
        var samples = new List<PerformanceSample>();
        for (var i = 1; i <= _sampleCount; i++)
        {
            var context = await CreateContextAsync(viewport);
            var page = await context.NewPageAsync();
            try
            {
                samples.Add(await MeasureAsync(
                    "cold-public-landing",
                    i,
                    TimeSpan.FromSeconds(60),
                    page,
                    async measuredPage =>
                    {
                        await measuredPage.GotoAsync(
                            fixture.Stack.AppBaseUrl + "/",
                            new() { WaitUntil = WaitUntilState.NetworkIdle });
                        await Assertions.Expect(new LandingPage(measuredPage).Heading)
                            .ToBeVisibleAsync(new() { Timeout = 15000 });
                        await WaitForNetworkIdleAsync(measuredPage);
                    }));
            }
            finally
            {
                await context.CloseAsync();
            }
        }

        AddResult(
            "cold-public-landing",
            TimeSpan.FromSeconds(60),
            viewport,
            "cold-context",
            "anonymous",
            samples);
    }

    private async Task MeasureExpiredSessionRedirectAsync(PerformanceViewport viewport)
    {
        var samples = new List<PerformanceSample>();
        for (var i = 1; i <= _sampleCount; i++)
        {
            var context = await CreateContextAsync(viewport);
            var page = await context.NewPageAsync();
            try
            {
                await AuthHelper.AuthenticateThroughOAuthAsync(
                    page,
                    fixture.Stack.AppBaseUrl);
                await WaitForNetworkIdleAsync(page);

                var original = (await context.CookiesAsync())
                    .First(cookie => cookie.Name == "battlenet_token");
                await context.AddCookiesAsync(
                [
                    new Cookie
                    {
                        Name = original.Name,
                        Value = original.Value,
                        Domain = original.Domain,
                        Path = original.Path,
                        HttpOnly = original.HttpOnly,
                        Secure = original.Secure,
                        SameSite = original.SameSite,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds(),
                    },
                ]);

                samples.Add(await MeasureAsync(
                    "expired-session-protected-route-redirect",
                    i,
                    TimeSpan.FromSeconds(30),
                    page,
                    async measuredPage =>
                    {
                        await measuredPage.GotoAsync(fixture.Stack.AppBaseUrl + "/runs");
                        await Assertions.Expect(measuredPage).ToHaveURLAsync(
                            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns"),
                            new() { Timeout = 15000 });
                        await Assertions.Expect(
                                measuredPage.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" }))
                            .ToBeVisibleAsync(new() { Timeout = 15000 });
                        await WaitForNetworkIdleAsync(measuredPage);
                    }));
            }
            finally
            {
                await context.CloseAsync();
            }
        }

        AddResult(
            "expired-session-protected-route-redirect",
            TimeSpan.FromSeconds(30),
            viewport,
            "warm-provider-session-expired-cookie",
            "expired-session",
            samples);
    }

    private async Task MeasureAuthenticatedRoutesAsync(PerformanceViewport viewport)
    {
        var context = await CreateAuthenticatedContextAsync(viewport);
        try
        {
            await MeasureAuthenticatedJourneyAsync(
                context,
                "authenticated-runs-load",
                TimeSpan.FromSeconds(60),
                viewport,
                null,
                async page =>
                {
                    var runsResponse = WaitForApiResponseAsync(page, "/api/v1/runs");
                    var runsPage = new RunsPage(page);
                    await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(runsPage.CreateRunButton)
                        .ToBeVisibleAsync(new() { Timeout = 15000 });
                    await runsResponse;
                    await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId))
                        .ToBeVisibleAsync(new() { Timeout = 15000 });
                    await WaitForNetworkIdleAsync(page);
                },
                measureRouteTransition: false);

            await MeasureAuthenticatedJourneyAsync(
                context,
                "run-create-form-load",
                TimeSpan.FromSeconds(45),
                viewport,
                null,
                async page =>
                {
                    var dependencyResponses = new[]
                    {
                        WaitForApiResponseAsync(page, "/api/v1/guild"),
                        WaitForApiResponseAsync(page, "/api/v1/wow/reference/expansions"),
                        WaitForApiResponseAsync(page, "/api/v1/wow/reference/instances"),
                    };
                    var runsPage = new RunsPage(page);
                    await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(runsPage.KeyLevelInput)
                        .ToBeVisibleAsync(new() { Timeout = 15000 });
                    await Task.WhenAll(dependencyResponses);
                    await WaitForNetworkIdleAsync(page);
                },
                measureRouteTransition: false);

            await MeasureAuthenticatedJourneyAsync(
                context,
                "characters-list-load",
                TimeSpan.FromSeconds(45),
                viewport,
                null,
                async page =>
                {
                    var charactersPage = new CharactersPage(page);
                    await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);
                    await Assertions.Expect(charactersPage.Heading)
                        .ToBeVisibleAsync(new() { Timeout = 15000 });
                    await WaitForNetworkIdleAsync(page);
                },
                measureRouteTransition: false);

            await MeasureAuthenticatedJourneyAsync(
                context,
                "warm-route-navigation",
                TimeSpan.FromSeconds(20),
                viewport,
                page => page.GotoAsync(
                    fixture.Stack.AppBaseUrl + "/runs",
                    new() { WaitUntil = WaitUntilState.NetworkIdle }),
                async page =>
                {
                    var navBar = new NavBar(page);
                    if (!await navBar.CharactersLink.IsVisibleAsync())
                    {
                        await page.GetByRole(AriaRole.Button, new() { Name = "Toggle navigation menu" })
                            .ClickAsync();
                        await Assertions.Expect(navBar.CharactersLink)
                            .ToBeVisibleAsync(new() { Timeout = 5000 });
                    }

                    await navBar.CharactersLink.ClickAsync();
                    await Assertions.Expect(new CharactersPage(page).Heading)
                        .ToBeVisibleAsync(new() { Timeout = 15000 });
                    await WaitForNetworkIdleAsync(page);
                },
                measureRouteTransition: true);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task<IBrowserContext> CreateAuthenticatedContextAsync(PerformanceViewport viewport)
    {
        var context = await CreateContextAsync(viewport);
        var authPage = await context.NewPageAsync();
        try
        {
            await AuthHelper.AuthenticateThroughOAuthAsync(
                authPage,
                fixture.Stack.AppBaseUrl);
        }
        catch
        {
            await context.CloseAsync();
            throw;
        }
        finally
        {
            await authPage.CloseAsync();
        }

        return context;
    }

    private Task<IBrowserContext> CreateContextAsync(PerformanceViewport viewport) =>
        fixture.Stack.Browser.NewContextAsync(new()
        {
            ViewportSize = new()
            {
                Width = viewport.Width,
                Height = viewport.Height,
            },
        });

    private async Task MeasureAuthenticatedJourneyAsync(
        IBrowserContext context,
        string name,
        TimeSpan budget,
        PerformanceViewport viewport,
        Func<IPage, Task>? setup,
        Func<IPage, Task> action,
        bool measureRouteTransition)
    {
        var samples = new List<PerformanceSample>();
        for (var i = 1; i <= _sampleCount; i++)
        {
            var page = await context.NewPageAsync();
            try
            {
                await StubCharacterPortraitsAsync(page);
                if (setup is not null)
                {
                    await setup(page);
                    await WaitForNetworkIdleAsync(page);
                }

                samples.Add(await MeasureAsync(
                    name,
                    i,
                    budget,
                    page,
                    action,
                    measureRouteTransition));
            }
            finally
            {
                await page.CloseAsync();
            }
        }

        AddResult(
            name,
            budget,
            viewport,
            measureRouteTransition ? "warm-spa-route" : "warm-auth-context-new-page",
            "authenticated",
            samples);
    }

    private static async Task StubCharacterPortraitsAsync(IPage page)
    {
        await page.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"portraits\":{}}",
            });
        });
    }

    private async Task<PerformanceSample> MeasureAsync(
        string name,
        int sample,
        TimeSpan budget,
        IPage page,
        Func<IPage, Task> action,
        bool measureRouteTransition = false)
    {
        var diagnostics = new BrowserDiagnostics(name);
        diagnostics.Attach(page);
        await PerformanceMetricsHelper.StartCollectionAsync(page);

        double? routeTransitionDurationMs = null;
        if (measureRouteTransition)
        {
            await StartRouteMeasureAsync(page);
        }

        var sw = Stopwatch.StartNew();
        await action(page);
        sw.Stop();

        if (measureRouteTransition)
        {
            routeTransitionDurationMs = await FinishRouteMeasureAsync(page);
        }

        var navigationDurationMs = measureRouteTransition
            ? null
            : await ReadNavigationDurationMsAsync(page);
        var browserMetrics = await PerformanceMetricsHelper.ReadAsync(page, routeTransitionDurationMs);

        return new PerformanceSample(
            sample,
            (long)sw.Elapsed.TotalMilliseconds,
            (long)budget.TotalMilliseconds,
            navigationDurationMs,
            routeTransitionDurationMs,
            browserMetrics,
            diagnostics.RequestFailures.ToArray(),
            diagnostics.HttpFailures.ToArray(),
            diagnostics.ConsoleErrors.ToArray());
    }

    private void AddResult(
        string name,
        TimeSpan budget,
        PerformanceViewport viewport,
        string cacheState,
        string userState,
        IReadOnlyList<PerformanceSample> samples)
    {
        var elapsedValues = samples.Select(sample => sample.ElapsedMs).ToArray();
        var routeValues = samples
            .Select(sample => sample.RouteTransitionDurationMs)
            .OfType<double>()
            .ToArray();
        var navigationValues = samples
            .Select(sample => sample.NavigationDurationMs)
            .OfType<double>()
            .ToArray();

        _results.Add(new PerformanceJourneyResult(
            name,
            viewport,
            cacheState,
            userState,
            PerformanceMetricsHelper.MetricSource,
            PerformanceMetricsHelper.GatePolicy,
            PerformanceMetricsHelper.ThresholdSource,
            (long)budget.TotalMilliseconds,
            PerformanceMetricsHelper.Percentile(elapsedValues, 50),
            PerformanceMetricsHelper.Percentile(elapsedValues, 75),
            elapsedValues.Max(),
            PercentileOrNull(navigationValues, 50),
            PercentileOrNull(navigationValues, 75),
            MaxOrNull(navigationValues),
            PercentileOrNull(routeValues, 50),
            PercentileOrNull(routeValues, 75),
            MaxOrNull(routeValues),
            PerformanceMetricsHelper.Summarize(samples.Select(sample => sample.BrowserMetrics).ToArray()),
            samples));
    }

    private static Task StartRouteMeasureAsync(IPage page) =>
        page.EvaluateAsync(
            """
            () => {
              performance.clearMarks("lfm-route-start");
              performance.clearMarks("lfm-route-end");
              performance.clearMeasures("lfm-route-transition");
              performance.mark("lfm-route-start");
            }
            """);

    private static async Task<double?> FinishRouteMeasureAsync(IPage page)
    {
        try
        {
            return await page.EvaluateAsync<double?>(
                """
                () => {
                  performance.mark("lfm-route-end");
                  performance.measure("lfm-route-transition", "lfm-route-start", "lfm-route-end");
                  const measure = performance.getEntriesByName("lfm-route-transition").at(-1);
                  return measure ? Math.round(measure.duration) : null;
                }
                """);
        }
        catch
        {
            return null;
        }
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

    private static Task WaitForNetworkIdleAsync(IPage page) =>
        page.WaitForLoadStateAsync(
            LoadState.NetworkIdle,
            new() { Timeout = 10000 });

    private static Task<IResponse> WaitForApiResponseAsync(IPage page, string path) =>
        page.WaitForResponseAsync(
            response => Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
                && uri.AbsolutePath == path,
            new() { Timeout = 15000 });

    private static long Percentile(IReadOnlyList<long> values, int percentile)
    {
        if (values.Count == 0)
            return 0;

        return (long)Math.Round(Percentile(values.Select(value => (double)value).ToArray(), percentile));
    }

    private static double? PercentileOrNull(IReadOnlyList<double> values, int percentile)
    {
        if (values.Count == 0)
            return null;

        return Math.Round(Percentile(values, percentile));
    }

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

    private static double? MaxOrNull(IReadOnlyList<double> values) =>
        values.Count == 0 ? null : Math.Round(values.Max());

    private static int ReadSampleCount()
    {
        var value = Environment.GetEnvironmentVariable("LFM_E2E_PERFORMANCE_SAMPLES");
        if (int.TryParse(value, out var samples))
            return Math.Clamp(samples, DefaultSampleCount, 10);

        return DefaultSampleCount;
    }

    private sealed class BrowserDiagnostics(string journey)
    {
        private readonly ConcurrentQueue<PerformanceDiagnostic> _requestFailures = new();
        private readonly ConcurrentQueue<PerformanceDiagnostic> _httpFailures = new();
        private readonly ConcurrentQueue<PerformanceDiagnostic> _consoleErrors = new();

        public IReadOnlyCollection<PerformanceDiagnostic> RequestFailures => _requestFailures.ToArray();
        public IReadOnlyCollection<PerformanceDiagnostic> HttpFailures => _httpFailures.ToArray();
        public IReadOnlyCollection<PerformanceDiagnostic> ConsoleErrors => _consoleErrors.ToArray();

        public void Attach(IPage page)
        {
            page.RequestFailed += (_, request) =>
            {
                if (IsAllowedRequestFailure(request))
                    return;

                _requestFailures.Enqueue(new(
                    "request-failed",
                    $"{request.Method} {request.Url} {request.Failure}"));
            };

            page.Response += (_, response) =>
            {
                if (response.Status < 400 || IsAllowedHttpFailure(response))
                    return;

                _httpFailures.Enqueue(new(
                    "http",
                    $"{response.Status} {response.Request.Method} {response.Url}"));
            };

            page.Console += (_, message) =>
            {
                if (message.Type != "error" || IsAllowedConsoleError(message.Text))
                    return;

                _consoleErrors.Enqueue(new("console", message.Text));
            };
        }

        private bool IsAllowedHttpFailure(IResponse response)
        {
            if (response.Status != 401
                || !Uri.TryCreate(response.Url, UriKind.Absolute, out var uri)
                || uri.AbsolutePath != "/api/v1/me")
            {
                return false;
            }

            return journey is "cold-public-landing"
                or "expired-session-protected-route-redirect";
        }

        private bool IsAllowedRequestFailure(IRequest request)
        {
            if (request.Failure?.Contains("net::ERR_ABORTED", StringComparison.OrdinalIgnoreCase) != true
                || !Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            // The SPA auth-state probe can be cancelled by route transitions or
            // context close after the page contract is visible. Completed
            // /api/v1/me 4xx/5xx responses still go through the HTTP check.
            if (uri.AbsolutePath == "/api/v1/me")
                return true;

            // Create-run loads reference/guild dependencies during render.
            // Playwright can report the fetch as aborted after the form is
            // already usable; HTTP responses on these paths are still checked.
            return journey == "run-create-form-load"
                && uri.AbsolutePath is "/api/v1/guild"
                    or "/api/v1/wow/reference/expansions"
                    or "/api/v1/wow/reference/instances";
        }

        private bool IsAllowedConsoleError(string text)
        {
            // Chromium can surface the same expected anonymous /api/v1/me 401
            // fetch as a generic console error without the URL in message text.
            return (journey is "cold-public-landing"
                    or "expired-session-protected-route-redirect")
                && text.Contains("401", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record PerformanceDiagnostic(string Type, string Message);

    private sealed record PerformanceViewport(string Name, int Width, int Height);

    private sealed record PerformanceSample(
        int Sample,
        long ElapsedMs,
        long BudgetMs,
        double? NavigationDurationMs,
        double? RouteTransitionDurationMs,
        BrowserPerformanceMetrics BrowserMetrics,
        IReadOnlyCollection<PerformanceDiagnostic> RequestFailures,
        IReadOnlyCollection<PerformanceDiagnostic> HttpFailures,
        IReadOnlyCollection<PerformanceDiagnostic> ConsoleErrors);

    private sealed record PerformanceRunMetadata(
        string StackTarget,
        string BrowserName,
        string BrowserVersion,
        string Runner,
        string OperatingSystem,
        string? Commit,
        string? Ref,
        int SampleCount,
        IReadOnlyList<PerformanceViewport> Viewports);

    private sealed record PerformanceReport(
        int SchemaVersion,
        DateTimeOffset GeneratedAt,
        PerformanceRunMetadata Run,
        string MetricSource,
        string GatePolicy,
        string ThresholdSource,
        IReadOnlyList<PerformanceJourneyResult> Journeys);

    private sealed record PerformanceJourneyResult(
        string Name,
        PerformanceViewport Viewport,
        string CacheState,
        string UserState,
        string MetricSource,
        string GatePolicy,
        string ThresholdSource,
        long BudgetMs,
        long P50ElapsedMs,
        long P75ElapsedMs,
        long MaxElapsedMs,
        double? P50NavigationDurationMs,
        double? P75NavigationDurationMs,
        double? MaxNavigationDurationMs,
        double? P50RouteTransitionDurationMs,
        double? P75RouteTransitionDurationMs,
        double? MaxRouteTransitionDurationMs,
        BrowserPerformanceMetricSummary BrowserMetrics,
        IReadOnlyList<PerformanceSample> Samples);

    private PerformanceRunMetadata CreateRunMetadata()
    {
        return new(
            "local-stack",
            ReadBrowserName(fixture.Stack.Browser),
            ReadBrowserVersion(fixture.Stack.Browser),
            ReadRunnerName(),
            Environment.OSVersion.Platform.ToString(),
            ReadFirstEnvironmentValue("GITHUB_SHA", "BUILD_SOURCEVERSION"),
            ReadFirstEnvironmentValue("GITHUB_REF", "GITHUB_HEAD_REF", "BUILD_SOURCEBRANCH"),
            _sampleCount,
            Viewports);
    }

    private static string ReadBrowserName(IBrowser browser)
    {
        var value = browser.BrowserType.Name;
        return string.IsNullOrWhiteSpace(value) ? "chromium" : value;
    }

    private static string ReadBrowserVersion(IBrowser browser)
    {
        var value = browser.Version;
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }

    private static string? ReadFirstEnvironmentValue(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string ReadRunnerName()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase))
            return "github-actions";

        return "local";
    }

    private void WriteReport()
    {
        var repoRoot = FindRepoRoot();
        var outputDir = Path.Combine(repoRoot, "artifacts", "e2e-results");
        Directory.CreateDirectory(outputDir);

        var report = new PerformanceReport(
            ReportSchemaVersion,
            DateTimeOffset.UtcNow,
            CreateRunMetadata(),
            PerformanceMetricsHelper.MetricSource,
            PerformanceMetricsHelper.GatePolicy,
            PerformanceMetricsHelper.ThresholdSource,
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
}
