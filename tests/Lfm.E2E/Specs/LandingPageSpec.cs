using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class LandingPageSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Root_route_renders_a_restrained_public_landing_page()
    {
        await _page.GotoAsync(fixture.AppBaseUrl);

        var main = _page.GetByRole(AriaRole.Main);

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"\/$"));
        // Blazor LandingPage renders "LFM" as H3 and "Looking For More" as H1
        await Expect(main.GetByText("LFM")).ToBeVisibleAsync();
        await Expect(main.GetByText("Looking For More")).ToBeVisibleAsync();
        await Expect(main.GetByText("Coordinate your guild's raid schedule")).ToBeVisibleAsync();
        // Navbar shows "Sign In" link for unauthenticated users
        await Expect(_page.Locator("fluent-anchor[href='/login']")).ToBeVisibleAsync();
        // Feature cards
        await Expect(main.GetByText("Shared Schedule")).ToBeVisibleAsync();
        await Expect(main.GetByText("Keep your whole raid on the same page")).ToBeVisibleAsync();
        await Expect(main.GetByText("Role Coverage")).ToBeVisibleAsync();
        await Expect(main.GetByText("See at a glance which roles are filled")).ToBeVisibleAsync();
        await Expect(main.GetByText("Battle.net Sign-In")).ToBeVisibleAsync();
        await Expect(main.GetByText("No extra accounts")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
