using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class NavbarSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    private const int MobileWidth = 390;
    private const int MobileHeight = 844;

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
    public async Task Desktop_navbar_keeps_routes_inline_and_exposes_characters_through_account_menu()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Guild" })).ToBeVisibleAsync();

        var trigger = _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex("Open navigation menu for", RegexOptions.IgnoreCase) });
        await trigger.ClickAsync();

        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Characters" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Logout" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Runs" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Signed_out_mobile_navbar_keeps_only_login_visible()
    {
        await _page.SetViewportSizeAsync(MobileWidth, MobileHeight);
        await _page.GotoAsync(fixture.AppBaseUrl);

        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Runs" })).ToHaveCountAsync(0);
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Guild" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Signed_in_mobile_navbar_collapses_routes_into_character_menu()
    {
        await _page.SetViewportSizeAsync(MobileWidth, MobileHeight);
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns&testAuthScenario=site-admin");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Runs" })).ToHaveCountAsync(0);

        var trigger = _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex("Open navigation menu for", RegexOptions.IgnoreCase) });
        await trigger.ClickAsync();

        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Characters" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Guild", Exact = true })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Guild Admin", Exact = true })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Menuitem, new() { Name = "Logout" })).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Menuitem, new() { Name = "Guild Admin" }).ClickAsync();
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild\/admin$"));
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
