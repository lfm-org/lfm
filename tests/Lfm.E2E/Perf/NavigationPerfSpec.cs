using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port of frontend/e2e/perf/navigation.perf.spec.ts — navigation responsiveness.
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
        var firstCard = _page.GetByTestId("run-card").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: firstCard);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Selecting_a_different_run_updates_the_detail_panel_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-edit-closed-deadmines");
        await Expect(_page.GetByText("Edit closed test run")).ToBeVisibleAsync();

        var targetButton = _page.GetByRole(AriaRole.Button,
            new() { NameRegex = new System.Text.RegularExpressions.Regex(@"Icecrown Citadel Heroic \(10 players\)") });
        var detailText = _page.GetByText("Closed progression lockout");

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => targetButton.ClickAsync(),
            ackMarker: detailText,
            completionMarker: detailText);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Heavy);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Fast);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Pagination_updates_run_list_within_budget()
    {
        // Desktop auto-select keeps a run= query pinned, which makes page-button clicks
        // a no-op for the visible list. Measure pagination under mobile layout where
        // the list view is not coupled to selection.
        await _page.SetViewportSizeAsync(390, 844);
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        await _page.GetByTestId("run-card").First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var page2Button = _page.GetByRole(AriaRole.Button, new() { Name = "2", Exact = true });
        var page2Content = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild ten-player alt run" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => page2Button.ClickAsync(),
            ackMarker: page2Content,
            completionMarker: page2Content);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Standard);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Fast);
        PerfHelper.AssertStableInteraction(result);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
