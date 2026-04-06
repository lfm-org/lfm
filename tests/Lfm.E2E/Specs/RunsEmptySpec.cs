using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("runs-empty")]
public class RunsEmptySpec(RunsEmptyFixture fixture) : IAsyncLifetime
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
    public async Task Runs_page_shows_empty_state_when_no_runs_are_seeded()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(_page.GetByText("No runs found.")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
