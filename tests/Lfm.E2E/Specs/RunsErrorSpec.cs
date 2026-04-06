using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("runs-error")]
public class RunsErrorSpec(RunsErrorFixture fixture) : IAsyncLifetime
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
    public async Task Runs_page_shows_error_when_runs_cannot_be_loaded()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(_page.GetByText("Failed to load runs")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
