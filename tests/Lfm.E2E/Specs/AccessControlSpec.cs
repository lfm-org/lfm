using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Pages;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("Default")]
public class AccessControlSpec(DefaultFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        _page = await _context.NewPageAsync();
        _page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
                System.Console.WriteLine($"[Browser {msg.Type.ToUpper()}] {msg.Text}");
        };
        _page.RequestFailed += (_, req) =>
            System.Console.WriteLine($"[Browser REQUESTFAILED] {req.Url} - {req.Failure}");
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromRuns()
    {
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(_page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(_page);
        await Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromCharacters()
    {
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(_page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fcharacters$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(_page);
        await Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromCreateRun()
    {
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs/new",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Expect(_page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns%2Fnew$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(_page);
        await Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Authenticated_CanAccessRuns()
    {
        var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var authPage = await authContext.NewPageAsync();

        try
        {
            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
                new() { WaitUntil = WaitUntilState.NetworkIdle });

            // Verify the page loaded (not redirected to login)
            await Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/runs$"));

            // Verify authenticated nav is visible — confirms auth state
            var navBar = new NavBar(authPage);
            await Expect(navBar.SignOutButton).ToBeVisibleAsync();
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    [Fact]
    public async Task PublicRoute_Unauthenticated_RendersWithoutRedirect()
    {
        // Landing page
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        _page.Url.Should().NotContain("/login?redirect");

        // Login page
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/login",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        var loginPage = new LoginPage(_page);
        await Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        _page.Url.Should().Contain("/login");
        _page.Url.Should().NotContain("redirect=");

        // Privacy page
        await _page.GotoAsync($"{fixture.Stack.AppBaseUrl}/privacy",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        _page.Url.Should().Contain("/privacy");
        _page.Url.Should().NotContain("/login?redirect");
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);

    private static IPageAssertions Expect(IPage page) =>
        Assertions.Expect(page);
}
