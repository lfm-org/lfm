using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildPermissionsSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Rank_two_guild_members_cannot_create_guild_runs_by_default()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns%2Fnew");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Guild" })).ToHaveCountAsync(0);
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Public" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Guild_masters_can_change_rank_permissions_and_blocked_ranks_lose_guild_signup_actions()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fguild&testAuthScenario=guild-master");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild$"));
        await _page.GetByLabel("Allow guild run creation for Rank 2").CheckAsync();
        await _page.GetByLabel("Allow guild run signup for Rank 2").UncheckAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save guild settings" }).ClickAsync();
        await Expect(_page.GetByText("Guild settings saved")).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex("Open navigation menu for", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await _page.GetByRole(AriaRole.Menuitem, new() { Name = "Logout" }).ClickAsync();
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login$"));

        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns%2Fnew");
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Guild" })).ToBeVisibleAsync();

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-guild-sparse-icc10");
        var signupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Guild ten-player alt run" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Guild ten-player alt run" });

        await Expect(signupRegion.GetByText("Guild signup is not enabled for your rank.")).ToBeVisibleAsync();
        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" })).ToHaveCountAsync(0);
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
