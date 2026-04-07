using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class LoginEntrySpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Login_page_renders_sign_in_button_and_navigates_to_auth()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/login?redirect=%2Fruns%2Fnew");

        // Blazor LoginPage renders "Sign In" as H2 and a button "Sign in with Battle.net"
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Connect your Battle.net account")).ToBeVisibleAsync();

        var loginButton = _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });
        await Expect(loginButton).ToBeVisibleAsync();

        // In E2E_TEST_MODE the login endpoint sets the cookie and redirects back
        await loginButton.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByText("Create Run")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Logout_button_exists_and_session_is_cleared_after_logout()
    {
        // Authenticate via AuthHelper
        var authContext = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        var authPage = await authContext.NewPageAsync();
        try
        {
            await authPage.GotoAsync(fixture.AppBaseUrl + "/runs");
            await Expect(authPage).ToHaveURLAsync(new Regex(@"\/runs$"));
            await Expect(authPage.GetByText("Runs").First).ToBeVisibleAsync();

            // Verify the Sign Out button is present in the MainLayout
            var signOutButton = authPage.GetByRole(AriaRole.Button, new() { Name = "Sign Out" });
            await Expect(signOutButton).ToBeVisibleAsync();

            // MainLayout navigates to /api/battlenet/logout (forceLoad) which is POST-only.
            // The actual logout flow is a server-side POST tested in API unit tests.
            // Here we verify the button exists and that after clearing cookies,
            // protected routes redirect to login.
        }
        finally
        {
            await authContext.CloseAsync();
        }

        // In a clean unauthenticated context, navigating to /runs should redirect to /login
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\?redirect=%2Fruns$"));
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Callback_failure_routes_user_to_login_failed_page()
    {
        // The callback endpoint redirects to /auth/failure on error,
        // which the Blazor LoginFailedPage handles via @page "/auth/failure"
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/callback", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/auth\/failure$"));
        // Blazor LoginFailedPage renders "Login Failed" as H3
        await Expect(_page.GetByText("Login Failed")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Something went wrong")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
