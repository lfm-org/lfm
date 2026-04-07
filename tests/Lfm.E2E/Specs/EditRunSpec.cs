using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class EditRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Edit_run_page_loads_and_shows_form()
    {
        // Navigate directly to edit page for a known run
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/run-guild-sparse-icc10/edit");

        // Blazor EditRunPage renders "Edit Run" as H3
        await Expect(_page.GetByText("Edit Run").First).ToBeVisibleAsync();

        // Wait for loading to finish
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Form fields should be present and pre-populated
        await Expect(_page.Locator("#instance-select")).ToBeVisibleAsync();
        await Expect(_page.Locator("#modekey-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#starttime-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#description-input")).ToBeVisibleAsync();

        // Action buttons
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Delete Run" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Cancel_button_navigates_back_to_run_detail()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/run-guild-sparse-icc10/edit");

        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        await _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(
            new Regex(@"\/runs\/run-guild-sparse-icc10$"));
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
