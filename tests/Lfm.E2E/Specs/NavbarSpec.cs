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
    public async Task Desktop_navbar_shows_nav_links_and_sign_out_when_authenticated()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));

        // Blazor MainLayout renders FluentAnchor links inline for authenticated users
        await Expect(_page.Locator("fluent-anchor[href='/runs']")).ToBeVisibleAsync();
        await Expect(_page.Locator("fluent-anchor[href='/guild']")).ToBeVisibleAsync();
        await Expect(_page.Locator("fluent-anchor[href='/characters']")).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Sign Out" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Signed_out_navbar_shows_sign_in_link()
    {
        await _page.GotoAsync(fixture.AppBaseUrl);

        // Unauthenticated: MainLayout shows a "Sign In" anchor
        await Expect(_page.Locator("fluent-anchor[href='/login']")).ToBeVisibleAsync();
        // Authenticated nav links should not be visible
        await Expect(_page.Locator("fluent-anchor[href='/characters']")).ToHaveCountAsync(0);
    }

    [Fact(Skip = "Blazor MainLayout does not have a mobile hamburger menu or Guild Admin link")]
    public async Task Signed_in_mobile_navbar_collapses_routes_into_character_menu()
    {
        await Task.CompletedTask;
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
