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

        var loginLink = _page.GetByRole(AriaRole.Link, new() { Name = "Continue with Battle.net" });
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" })).ToBeVisibleAsync();
        await Expect(loginLink).ToHaveAttributeAsync("href", "/api/battlenet/login?redirect=%2Fruns%2Fnew");

        await loginLink.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Local_test_mode_login_routes_raider_without_character_through_character_selection()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns%2Fnew&testAuthScenario=needs-character");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/characters\?redirect=%2Fruns%2Fnew$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Select your character" })).ToBeVisibleAsync();

        // Use Filter with regex because the button label includes the character name "Aelrin" plus extra text.
        await _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex("Aelrin") })
            .ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/new$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Logout_clears_session_and_protects_runs_again()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex("Open navigation menu for", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await _page.GetByRole(AriaRole.Menuitem, new() { Name = "Logout" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" })).ToBeVisibleAsync();

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\?redirect=%2Fruns$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Callback_failure_routes_user_to_login_failed_page()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/callback", new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\/failed$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in failed" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Retry login" })).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
