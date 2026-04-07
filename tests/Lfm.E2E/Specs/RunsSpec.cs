using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class RunsSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Authenticated_runs_page_shows_run_list_and_detail_panel()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        // Blazor RunsPage renders "Runs" as H3 heading
        await Expect(_page.GetByText("Runs").First).ToBeVisibleAsync();

        // "Create Run" and "Refresh" buttons are visible
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Create Run" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Refresh" })).ToBeVisibleAsync();

        // Wait for loading to complete (progress ring disappears)
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // The run list sidebar should show instance names (rendered as <strong>)
        // and the prompt to select a run
        await Expect(_page.GetByText("Select a run to see details.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigating_to_run_by_id_shows_detail_panel()
    {
        // Navigate directly to a known run's detail page via URL parameter
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/run-guild-sparse-icc10");

        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // The detail panel should show the run's instance name
        await Expect(_page.GetByText("Icecrown Citadel")).ToBeVisibleAsync();

        // Detail panel should show Mode and Visibility fields
        await Expect(_page.GetByText("Mode:")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Visibility:")).ToBeVisibleAsync();

        // Edit button in the detail panel
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
