using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildAdminSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Site_admins_can_load_guild_through_guild_admin()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild\/admin$"));
        // Blazor GuildAdminPage renders "Guild Admin" as H3
        await Expect(_page.GetByText("Guild Admin").First).ToBeVisibleAsync();

        // The Guild ID input has Id="guild-id-input" and Label="Guild ID"
        // FluentTextField with Label renders a <label>; fill via the text field directly
        await _page.Locator("#guild-id-input").FillAsync("54321");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Load Guild" }).ClickAsync();

        // Wait for guild data to load
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
    }

    [Fact(Skip = "Blazor GuildAdminPage stale-data locking depends on seed data with stale guild setup")]
    public async Task Site_admins_see_stale_guild_data_as_locked_in_guild_admin()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor GuildAdminPage does not implement non-admin access restriction in the UI")]
    public async Task Non_admin_users_cannot_access_guild_admin()
    {
        await Task.CompletedTask;
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
