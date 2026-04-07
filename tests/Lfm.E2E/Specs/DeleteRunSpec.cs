using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class DeleteRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Confirmation_dialog_cancel_preserves_the_run()
    {
        // Navigate to edit page for a known run created by the test user
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/run-public-empty-deadmines/edit");

        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Click "Delete Run" to show confirmation
        await _page.GetByRole(AriaRole.Button, new() { Name = "Delete Run" }).ClickAsync();

        // Confirm dialog appears
        await Expect(_page.GetByText("Confirm Delete")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Are you sure you want to delete this run?")).ToBeVisibleAsync();

        // Cancel the delete
        var cancelButtons = _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });
        // The second Cancel button is in the delete confirmation card
        await cancelButtons.Last.ClickAsync();

        // Confirmation should disappear, but we're still on the edit page
        await Expect(_page.GetByText("Confirm Delete")).ToHaveCountAsync(0);
        await Expect(_page.GetByText("Edit Run").First).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
