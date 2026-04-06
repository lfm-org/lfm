using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port — navigation responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class NavigationPerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    [Trait("Category", "Perf")]
    public async Task Runs_list_loads_within_budget()
    {
        var main = _page.GetByRole(AriaRole.Main);
        // Blazor RunsPage: wait for the heading "Runs" as completion marker
        var runsHeading = _page.GetByText("Runs").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: runsHeading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact(Skip = "Blazor RunsPage does not have run-card testid or sidebar button selection with detail panel text")]
    [Trait("Category", "Perf")]
    public async Task Selecting_a_different_run_updates_the_detail_panel_within_budget()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not implement pagination")]
    [Trait("Category", "Perf")]
    public async Task Pagination_updates_run_list_within_budget()
    {
        await Task.CompletedTask;
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
