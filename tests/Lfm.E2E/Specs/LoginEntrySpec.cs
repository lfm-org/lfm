using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Pages;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("Default")]
public class LoginEntrySpec(DefaultFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task LoginPage_Renders_ShowsSignInButton()
    {
        var loginPage = new LoginPage(_page);

        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        var visible = await loginPage.IsSignInButtonVisibleAsync();
        visible.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_ClickButton_RedirectsToBattleNetOAuth()
    {
        var loginPage = new LoginPage(_page);

        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        // forceLoad: true navigates to /api/battlenet/login, which immediately
        // redirects to the external Battle.net OAuth URL (unavailable in test).
        // Wait for the request to /api/battlenet/login to be initiated, which
        // confirms the button wired up the correct navigation URL.
        var loginRequestTask = _page.WaitForRequestAsync(
            new System.Text.RegularExpressions.Regex(@"/api/battlenet/login"),
            new() { Timeout = 10000 });

        await loginPage.ClickSignInAsync();

        await loginRequestTask;
    }

    [Fact]
    public async Task TestModeLogin_ValidIdentity_SetsCookieAndRedirects()
    {
        var loginUrl = $"{fixture.Stack.ApiBaseUrl}/api/e2e/login"
            + $"?battleNetId=test-bnet-id"
            + $"&redirect={Uri.EscapeDataString("/runs")}";

        await _page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        await _page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs"),
            new() { Timeout = 15000 });

        // Verify auth cookie was set — nav bar shows Sign Out
        var navBar = new NavBar(_page);
        await Expect(navBar.SignOutButton).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Logout_ClickSignOut_ClearsSessionAndRedirects()
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

            var navBar = new NavBar(authPage);
            await Expect(navBar.SignOutButton).ToBeVisibleAsync();

            // Sign out navigates to /api/battlenet/logout (forceLoad)
            // which clears the cookie and redirects to app base URL
            await navBar.ClickSignOutAsync();

            await authPage.WaitForURLAsync(
                new System.Text.RegularExpressions.Regex(@"^http://localhost:\d+/?$"),
                new() { Timeout = 15000 });

            // Verify session cleared — protected route redirects to login
            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
                new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns"));
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    [Fact]
    public async Task AuthFailure_NavigateToErrorPage_ShowsErrorMessage()
    {
        var errorPage = new LoginFailedPage(_page);

        await errorPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Expect(errorPage.ErrorHeading).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(errorPage.ErrorMessage).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(errorPage.TryAgainButton).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Assertions.Expect(locator);

    private static IPageAssertions Expect(IPage page) =>
        Assertions.Expect(page);
}
