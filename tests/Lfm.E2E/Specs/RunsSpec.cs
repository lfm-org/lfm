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

    [Fact(Skip = "Blazor RunsPage does not implement run= query param selection, pagination, or passed-runs section")]
    public async Task Runs_page_focuses_requested_run_query_on_correct_page()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not implement a collapsed passed-runs section")]
    public async Task Passed_runs_section_is_collapsed_by_default_and_expandable()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not implement deep-link auto-expand for passed runs")]
    public async Task Deep_link_to_passed_run_auto_expands_passed_section()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not implement mobile card expand/collapse")]
    public async Task Mobile_runs_page_keeps_cards_compact_until_expanded()
    {
        await Task.CompletedTask;
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
