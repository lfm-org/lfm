using Lfm.E2E.Fixtures;
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
    public async Task Local_test_mode_login_redirects_configured_raider_to_requested_route()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/login?redirect=%2Fruns%2Fnew");

        // Blazor LoginPage renders "Sign In" as H2 and a button "Sign in with Battle.net"
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Connect your Battle.net account")).ToBeVisibleAsync();

        // The login button triggers a navigation to /api/battlenet/login via OnClick.
        // In the Blazor app, the button itself navigates (not a link).
        var loginButton = _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });
        await Expect(loginButton).ToBeVisibleAsync();
        await loginButton.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByText("Create Run")).ToBeVisibleAsync();
    }

    [Fact(Skip = "Blazor LoginPage does not support needs-character flow via testAuthScenario")]
    public async Task Local_test_mode_login_routes_raider_without_character_through_character_selection()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Logout_clears_session_and_protects_runs_again()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));
        await Expect(_page.GetByText("Runs")).ToBeVisibleAsync();

        // Blazor MainLayout has a "Sign Out" button in the header (not a dropdown menu)
        await _page.GetByRole(AriaRole.Button, new() { Name = "Sign Out" }).ClickAsync();

        // After logout, navigating to /runs should redirect to /login
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\?redirect=%2Fruns$"));
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Callback_failure_routes_user_to_login_failed_page()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/callback", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\/failed$"));
        // Blazor LoginFailedPage renders "Login Failed" as H3
        await Expect(_page.GetByText("Login Failed")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Something went wrong")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
