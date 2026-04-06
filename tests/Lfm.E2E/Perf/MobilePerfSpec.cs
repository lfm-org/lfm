using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port — mobile responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class MobilePerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private const int MobileWidth = 390;
    private const int MobileHeight = 844;

    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _page = await _context.NewPageAsync();
        await _page.SetViewportSizeAsync(MobileWidth, MobileHeight);
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Mobile_runs_list_loads_within_budget()
    {
        var main = _page.GetByRole(AriaRole.Main);
        var runsHeading = _page.GetByText("Runs").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: runsHeading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Mobile);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact(Skip = "Blazor RunsPage does not implement mobile card expand/collapse or inline signup region")]
    [Trait("Category", "Perf")]
    public async Task Mobile_card_expand_shows_details_within_budget()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not implement mobile inline signup flow")]
    [Trait("Category", "Perf")]
    public async Task Mobile_run_signup_shows_busy_state_and_completes_within_budget()
    {
        await Task.CompletedTask;
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
