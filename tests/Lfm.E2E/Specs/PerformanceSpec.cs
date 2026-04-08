using System.Diagnostics;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Performance")]
[Trait("Category", "Perf")]
public class PerformanceSpec(PerformanceFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Context is not null)
            await Context.CloseAsync();
    }

    [Fact]
    public async Task LandingPage_LoadPerformance()
    {
        await PerfHelper.InjectPerformanceObserverAsync(Page!);
        var tracker = PerfHelper.StartApiTracking(Page!);

        await Page!.GotoAsync(fixture.Stack.AppBaseUrl,
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var apiCalls = PerfHelper.StopApiTracking(tracker);
        var metrics = await PerfHelper.ExtractTimingMetricsAsync(Page, apiCalls);

        PerfResultCollector.Record("LandingPage_Load", metrics);

        var formatted = PerfHelper.FormatPerfOutput("LandingPage_LoadPerformance", metrics);
        Log(formatted);
    }

    [Fact]
    public async Task AuthFlow_LoginToAuthenticatedPage()
    {
        // Create a fresh anonymous context/page for this test.
        var sw = Stopwatch.StartNew();

        var loginUrl = $"{fixture.Stack.ApiBaseUrl}/api/e2e/login"
            + $"?battleNetId={Uri.EscapeDataString(DefaultSeed.PrimaryBattleNetId)}"
            + $"&redirect={Uri.EscapeDataString("/runs")}";

        await PerfHelper.InjectPerformanceObserverAsync(Page!);
        var tracker = PerfHelper.StartApiTracking(Page!);

        await Page!.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs"),
            new() { Timeout = 15000 });

        sw.Stop();

        var apiCalls = PerfHelper.StopApiTracking(tracker);
        var pageMetrics = await PerfHelper.ExtractTimingMetricsAsync(Page, apiCalls);

        var metrics = pageMetrics with { NavigationTime = sw.Elapsed.TotalMilliseconds };
        PerfResultCollector.Record("AuthFlow_LoginToAuthenticated", metrics);

        var formatted = PerfHelper.FormatPerfOutput("AuthFlow_LoginToAuthenticatedPage", metrics);
        Log(formatted);
    }

    [Fact]
    public async Task RunsPage_LoadPerformance()
    {
        // Authenticate first, then measure runs page load.
        await Context!.CloseAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();

        await PerfHelper.InjectPerformanceObserverAsync(Page);
        var tracker = PerfHelper.StartApiTracking(Page);

        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var apiCalls = PerfHelper.StopApiTracking(tracker);
        var metrics = await PerfHelper.ExtractTimingMetricsAsync(Page, apiCalls);

        PerfResultCollector.Record("RunsPage_Load", metrics);

        var formatted = PerfHelper.FormatPerfOutput("RunsPage_LoadPerformance", metrics);
        Log(formatted);
    }

    [Fact]
    public async Task CharactersPage_LoadPerformance()
    {
        await Context!.CloseAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();

        await PerfHelper.InjectPerformanceObserverAsync(Page);
        var tracker = PerfHelper.StartApiTracking(Page);

        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var apiCalls = PerfHelper.StopApiTracking(tracker);
        var metrics = await PerfHelper.ExtractTimingMetricsAsync(Page, apiCalls);

        PerfResultCollector.Record("CharactersPage_Load", metrics);

        var formatted = PerfHelper.FormatPerfOutput("CharactersPage_LoadPerformance", metrics);
        Log(formatted);
    }

    [Fact]
    public async Task RunDetail_LoadPerformance()
    {
        await Context!.CloseAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();

        await PerfHelper.InjectPerformanceObserverAsync(Page);
        var tracker = PerfHelper.StartApiTracking(Page);

        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs/{DefaultSeed.TestRunId}",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var apiCalls = PerfHelper.StopApiTracking(tracker);
        var metrics = await PerfHelper.ExtractTimingMetricsAsync(Page, apiCalls);

        PerfResultCollector.Record("RunDetail_Load", metrics);

        var formatted = PerfHelper.FormatPerfOutput("RunDetail_LoadPerformance", metrics);
        Log(formatted);
    }

    [Fact]
    public async Task SpaNavigation_RouteTransitionTimes()
    {
        await Context!.CloseAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();

        // Start at /runs (full navigation to establish initial page load).
        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var navBar = new NavBar(Page);

        // SPA transition: /runs → /characters via nav link click.
        var sw1 = Stopwatch.StartNew();
        await navBar.CharactersLink.ClickAsync();
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });
        sw1.Stop();
        var runsToCharacters = sw1.Elapsed.TotalMilliseconds;

        // SPA transition: /characters → /guild via nav link click.
        var sw2 = Stopwatch.StartNew();
        await navBar.GuildLink.ClickAsync();
        await Page.WaitForSelectorAsync("h1", new() { Timeout = 10000 });
        sw2.Stop();
        var charactersToGuild = sw2.Elapsed.TotalMilliseconds;

        Log($"[PERF] SpaNavigation_RouteTransitionTimes");
        Log($"  /runs → /characters:  {runsToCharacters:N0} ms");
        Log($"  /characters → /guild: {charactersToGuild:N0} ms");

        PerfResultCollector.Record("SpaNavigation_RunsToCharacters",
            new PerfMetrics(NavigationTime: runsToCharacters));
        PerfResultCollector.Record("SpaNavigation_CharactersToGuild",
            new PerfMetrics(NavigationTime: charactersToGuild));
    }
}
