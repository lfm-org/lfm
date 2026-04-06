using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildHomeSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Authenticated_guild_members_can_open_the_read_only_guild_home()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        // Blazor MainLayout renders "Guild" as a FluentAnchor
        await _page.Locator("fluent-anchor[href='/guild']").ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild$"));
        // Blazor GuildPage renders "Guild" as H3
        await Expect(_page.GetByText("Guild").First).ToBeVisibleAsync();

        // Wait for loading to finish
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Guild name should be visible (from seed data)
        await Expect(_page.GetByText("Test Guild")).ToBeVisibleAsync();
    }

    [Fact(Skip = "Blazor GuildPage does not implement guild-master setup flow with slogan/timezone editing")]
    public async Task Guild_masters_can_save_slogan_and_timezone_before_entering_raids()
    {
        await Task.CompletedTask;
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
