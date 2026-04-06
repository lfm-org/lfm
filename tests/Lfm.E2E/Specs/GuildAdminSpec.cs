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
    public async Task Site_admins_can_resolve_a_guild_explicitly_and_edit_its_settings_through_guild_admin()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild\/admin$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Guild admin" })).ToBeVisibleAsync();

        await _page.GetByLabel("Guild ID").FillAsync("54321");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Load guild" }).ClickAsync();

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Rival Guild" })).ToBeVisibleAsync();
        await _page.GetByLabel("Slogan").FillAsync("Bench starts on time.");
        await _page.GetByLabel("Allow guild run creation for Rank 2").CheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save guild settings" }).ClickAsync();

        await Expect(_page.GetByText("Guild settings saved")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Slogan")).ToHaveValueAsync("Bench starts on time.");
    }

    [Fact]
    public async Task Site_admins_see_stale_guild_data_as_locked_in_guild_admin()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fguild%2Fadmin&testAuthScenario=site-admin");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild\/admin$"));

        await _page.GetByLabel("Guild ID").FillAsync("65432");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Load guild" }).ClickAsync();

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Stale Vanguard" })).ToBeVisibleAsync();
        await Expect(_page.GetByText(
            "Rank sync is stale. Guild settings are locked until roster data refreshes.")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Slogan")).ToBeDisabledAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Save guild settings" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Non_admin_users_cannot_access_guild_admin()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fguild%2Fadmin");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild\/admin$"));
        await Expect(_page.GetByText("Site admin access required.")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Guild ID")).ToHaveCountAsync(0);
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
