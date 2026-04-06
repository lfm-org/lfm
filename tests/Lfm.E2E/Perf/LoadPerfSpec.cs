using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port of frontend/e2e/perf/load.perf.spec.ts — entry and load responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data).
/// </summary>
[Collection("default")]
public class LoadPerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Landing_page_loads_within_budget()
    {
        var main = _page.GetByRole(AriaRole.Main);
        var heading = _page.GetByRole(AriaRole.Heading, new() { Name = "Plan runs in one place" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(fixture.AppBaseUrl + "/", new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: heading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Fast);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Login_page_loads_within_budget()
    {
        var main = _page.GetByRole(AriaRole.Main);
        var heading = _page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(
                fixture.AppBaseUrl + "/login?redirect=%2Fruns",
                new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: heading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Fast);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Login_click_transitions_to_authenticated_state_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/login?redirect=%2Fruns");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" }))
            .ToBeVisibleAsync();

        var loginLink = _page.GetByRole(AriaRole.Link, new() { Name = "Continue with Battle.net" });
        var runsHeading = _page.GetByRole(AriaRole.Heading, new() { Name = "Runs" });

        // Note: login click triggers a full-page navigation through /api/battlenet/login.
        // Browser observers installed before the click are lost when the page context changes.
        // Stability data reflects only the post-redirect render. This is acceptable —
        // the redirect is server-side, and we care about the user-visible authenticated landing state.
        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => loginLink.ClickAsync(),
            ackMarker: runsHeading,
            completionMarker: runsHeading);

        await ExpectPage(_page).ToHaveURLAsync(new Regex(@"\/runs(?:\?.*)?$"));

        PerfHelper.AssertAcknowledgementWithin(result, CompletionBudget.Redirect);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Redirect);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Login_click_transitions_to_protected_create_run_route_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/login?redirect=%2Fruns%2Fnew");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" }))
            .ToBeVisibleAsync();

        var loginLink = _page.GetByRole(AriaRole.Link, new() { Name = "Continue with Battle.net" });
        var createRunHeading = _page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => loginLink.ClickAsync(),
            ackMarker: createRunHeading,
            completionMarker: createRunHeading);

        await ExpectPage(_page).ToHaveURLAsync(new Regex(@"\/runs\/new(?:\?.*)?$"));

        PerfHelper.AssertAcknowledgementWithin(result, CompletionBudget.Redirect);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Redirect);
        PerfHelper.AssertStableInteraction(result);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);

    private static IPageAssertions ExpectPage(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);
}
