// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Auth")]
[Trait("Category", E2ELanes.Functional)]
public class AuthSpec(AuthFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    // 401 + /api/v1/me — expected for the anonymous context. MONO_WASM /
    // .wasm / mono_download_assets — intermittent Blazor WASM bundle download
    // flake that hits cold-start forceLoad redirects (e.g. /api/v1/battlenet/login);
    // unrelated to the assertions these tests make. See #45.
    protected override string[] IgnoredConsolePatterns =>
        ["401", "/api/v1/me", "MONO_WASM", ".wasm", "mono_download_assets"];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
        await StartTracingAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Context is not null)
            await Context.CloseAsync();
    }

    // E2E scope: proves the sign-in button starts browser navigation to Battle.net OAuth.
    // Cheaper lanes cannot prove this because the contract is a force-load browser request.
    // Shared data: none.
    [Fact]
    public async Task SignIn_ClickButton_RedirectsToBattleNetOAuth()
    {
        var loginPage = new LoginPage(Page!);

        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        // forceLoad: true navigates to /api/v1/battlenet/login, which immediately
        // redirects to the external Battle.net OAuth URL (unavailable in test).
        // Wait for the request to the Battle.net login endpoint to be initiated, which
        // confirms the button wired up the correct navigation URL.
        var loginRequestTask = Page!.WaitForRequestAsync(
            new System.Text.RegularExpressions.Regex(@"/api/(?:v1/)?battlenet/login"),
            new() { Timeout = 10000 });

        await loginPage.ClickSignInAsync();

        await loginRequestTask;

        // Close the page instead of parking on about:blank:
        //   1. Cancels the pending navigation to the unreachable Battle.net
        //      OAuth URL so the 404 cascade for its offline assets never
        //      reaches the console.
        //   2. Keeps the origin on localhost; Chrome 147's Private Network
        //      Access enforcement would otherwise retroactively flag the
        //      prior loopback favicon load once the origin switched to
        //      `null` via about:blank (#58).
        // Nulling Page lets the E2ETestBase DisposeAsync skip its post-test
        // screenshot/trace capture on the now-closed page.
        await Page.CloseAsync();
        Page = null;
    }

    // E2E scope: proves test-mode login creates a browser session and lands on the redirect target.
    // Cheaper lanes cannot prove this because cookie storage and authorized nav rendering require a browser.
    // Shared data: read-only.
    [Fact]
    public async Task TestModeLogin_ValidIdentity_SetsCookieAndRedirects()
    {
        var loginUrl = $"{fixture.Stack.ApiBaseUrl}/api/e2e/login"
            + $"?battleNetId=test-bnet-id"
            + $"&redirect={Uri.EscapeDataString("/runs")}";

        await Page!.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs"),
            new() { Timeout = 15000 });

        // Verify auth cookie was set — nav bar shows Sign Out. Explicit 15s
        // timeout because Blazor WASM's <fluent-button> component upgrade can
        // lag behind the /runs navigation on a cold CI runner (see #45).
        var navBar = new NavBar(Page);
        await Assertions.Expect(navBar.AccountMenuButton).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    // E2E scope: proves sign-out clears the browser session and protected routes redirect.
    // Cheaper lanes cannot prove this because the force-load logout and SPA re-bootstrap are browser behavior.
    // Shared data: read-only.
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
            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs");

            var navBar = new NavBar(authPage);
            // Explicit 15s timeout — <fluent-button> component upgrade can lag
            // behind Blazor bootstrap on a cold CI runner (see #45).
            await Assertions.Expect(navBar.AccountMenuButton).ToBeVisibleAsync(new() { Timeout = 15000 });

            // Sign out navigates to /api/battlenet/logout (forceLoad)
            // which clears the cookie and redirects to app base URL.
            await navBar.ClickSignOutAsync();

            // Use the auto-retrying ToHaveURLAsync assertion instead of WaitForURLAsync:
            // WaitUntilState.NetworkIdle does not settle cleanly through the forceLoad
            // redirect + Blazor WASM re-bootstrap chain, which previously timed out.
            await Assertions.Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"^http://localhost:\d+/?$"),
                new() { Timeout = 15000 });

            // Verify session cleared — revisit a protected route and confirm the
            // SPA redirects to /login. Goto's default WaitUntil=Load plus the
            // auto-retrying URL assertion give Blazor enough time to fetch
            // /api/v1/me (now 401) and fire RedirectToLogin.
            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs");
            await Assertions.Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns"),
                new() { Timeout = 15000 });
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    // E2E scope: proves the login failure route renders its recovery UI in the browser.
    // Cheaper lanes cannot prove this because route activation and rendered Fluent UI are browser-observable.
    // Shared data: none.
    [Fact]
    public async Task AuthFailure_NavigateToErrorPage_ShowsErrorMessage()
    {
        var errorPage = new LoginFailedPage(Page!);

        await errorPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(errorPage.ErrorHeading).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(errorPage.ErrorMessage).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(errorPage.TryAgainButton).ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
