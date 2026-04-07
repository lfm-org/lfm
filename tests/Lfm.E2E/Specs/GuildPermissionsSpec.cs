using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildPermissionsSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Create_run_page_has_visibility_select_with_public_and_guild_options()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");

        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Visibility dropdown is present
        var visibilitySelect = _page.Locator("#visibility-select");
        await Expect(visibilitySelect).ToBeVisibleAsync();

        // Verify options exist (Public and Guild only)
        await Expect(_page.Locator("fluent-option[value='PUBLIC']")).ToHaveCountAsync(1);
        await Expect(_page.Locator("fluent-option[value='GUILD']")).ToHaveCountAsync(1);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
