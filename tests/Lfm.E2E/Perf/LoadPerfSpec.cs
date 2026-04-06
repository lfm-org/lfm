using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port — entry and load responsiveness.
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
        // Blazor LandingPage renders "Looking For More" as the prominent heading
        var heading = _page.GetByText("Looking For More");

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
        // Blazor LoginPage renders "Sign In" as H2
        var heading = _page.GetByText("Sign In");

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
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();

        // Blazor LoginPage has a button (not a link) for sign-in
        var loginButton = _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });
        var runsHeading = _page.GetByText("Runs").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => loginButton.ClickAsync(),
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
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();

        var loginButton = _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });
        var createRunHeading = _page.GetByText("Create Run").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => loginButton.ClickAsync(),
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
