using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class NavbarSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _unauthContext = null!;
    private IPage _unauthPage = null!;
    private IBrowserContext _authContext = null!;
    private IPage _authPage = null!;

    public async Task InitializeAsync()
    {
        _unauthContext = await fixture.Browser.NewContextAsync();
        _unauthPage = await _unauthContext.NewPageAsync();

        _authContext = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _authPage = await _authContext.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _unauthContext.CloseAsync();
        await _authContext.CloseAsync();
    }

    [Fact]
    public async Task Desktop_navbar_shows_nav_links_and_sign_out_when_authenticated()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/runs");

        await Expect(_authPage).ToHaveURLAsync(new Regex(@"\/runs$"));

        // Blazor MainLayout renders FluentAnchor links inline for authenticated users
        await Expect(_authPage.Locator("fluent-anchor[href='/runs']")).ToBeVisibleAsync();
        await Expect(_authPage.Locator("fluent-anchor[href='/guild']")).ToBeVisibleAsync();
        await Expect(_authPage.Locator("fluent-anchor[href='/characters']")).ToBeVisibleAsync();
        await Expect(_authPage.GetByRole(AriaRole.Button, new() { Name = "Sign Out" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Signed_out_navbar_shows_sign_in_link()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl);

        // Unauthenticated: MainLayout shows a "Sign In" anchor
        await Expect(_unauthPage.Locator("fluent-anchor[href='/login']")).ToBeVisibleAsync();
        // Authenticated nav links should not be visible
        await Expect(_unauthPage.Locator("fluent-anchor[href='/characters']")).ToHaveCountAsync(0);
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
