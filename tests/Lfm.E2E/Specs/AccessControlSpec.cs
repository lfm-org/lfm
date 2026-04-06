using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class AccessControlSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Unauthenticated_protected_routes_redirect_to_the_themed_login_page()
    {
        foreach (var protectedPath in new[] { "/runs", "/characters", "/runs/new" })
        {
            await _page.GotoAsync(
                fixture.AppBaseUrl + protectedPath,
                new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            var encodedPath = Uri.EscapeDataString(protectedPath);
            await Expect(_page).ToHaveURLAsync(new Regex($@"/login\?redirect={Regex.Escape(encodedPath)}$"));
            // Blazor LoginPage renders "Sign In" as H2
            await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
        }
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
