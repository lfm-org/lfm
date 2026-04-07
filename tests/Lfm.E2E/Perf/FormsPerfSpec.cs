using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port -- form responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class FormsPerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Create_run_page_loads_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/");
        await Expect(_page.GetByText("Looking For More")).ToBeVisibleAsync();

        var main = _page.GetByRole(AriaRole.Main);
        var heading = _page.GetByText("Create Run").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(
                fixture.AppBaseUrl + "/runs/new",
                new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: heading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Edit_run_page_loads_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        var main = _page.GetByRole(AriaRole.Main);
        var heading = _page.GetByText("Edit Run").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(
                fixture.AppBaseUrl + "/runs/run-guild-sparse-icc10/edit",
                new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: heading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
