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

        // Blazor RunsPage renders "Runs" as H3
        await Expect(_page.GetByText("Runs").First).ToBeVisibleAsync();
        // The error state renders in a FluentMessageBar; the exact message comes from
        // the exception message. Check for the message bar with error intent.
        await Expect(_page.Locator("fluent-message-bar")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
