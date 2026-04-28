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

// Smoke test for the production Battle.net OAuth callback path.
//
// Every other authenticated test in the suite short-circuits the OAuth flow
// via `/api/e2e/login` (a test-only endpoint that skips the real callback).
// That leaves `BattleNetCallbackFunction` — the production code path users
// actually hit — with zero E2E coverage; a regression there would only surface
// when a real user tried to sign in.
//
// This spec drives the **production** callback path end-to-end against a local
// OAuth stub:
//   1. Browser navigates to /login and clicks Sign in with Battle.net
//   2. API's BattleNetLoginFunction redirects to the WireMock /oauth/authorize
//   3. WireMock returns a 302 back to the API callback with a fake code
//      and the state echoed from the authorize request
//   4. API's BattleNetCallbackFunction exchanges the code for a token
//      (WireMock /oauth/token), fetches the user (WireMock /oauth/userinfo),
//      upserts the raider, sets the auth cookie, and redirects to the app
//   5. Browser lands on the authenticated home page
//
// The test asserts the end of the flow (signed-in navbar on /runs) — the
// assertion target that proves the contract users actually care about.
[Collection("AuthCallback")]
[Trait("Category", "Functional")]
public class AuthCallbackSpec(AuthCallbackFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    // See AuthSpec for the rationale on the WASM patterns — this spec
    // exercises the same cold-start Blazor bundle download path.
    protected override string[] IgnoredConsolePatterns =>
        ["401", "503", "/api/me", "MONO_WASM", ".wasm", "mono_download_assets"];

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

    [Fact]
    public async Task ProductionCallback_StubbedOAuthProvider_SignsInAndLandsOnRuns()
    {
        var loginPage = new LoginPage(Page!);

        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        // 30s timeout — this is typically the first test in the suite to hit
        // the /login page on a cold CI runner, so the Blazor WASM bootstrap +
        // FluentUI component registration + the <fluent-button> upgrade all
        // happen inside this wait. Sibling AuthSpec.LoginPage_Renders hits a
        // warm cache and can get by with 10s; this one cannot. See #45.
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 30000 });

        // Click the real sign-in button. In production this hits /api/battlenet/login
        // which redirects through the Battle.net OAuth authorize / token / userinfo
        // endpoints. Locally, Blizzard__OAuthBaseUrl points at the WireMock stub,
        // so every outbound Battle.net call resolves against the stub instead of
        // the real provider — but the API code path (state generation, cookie
        // protection, code exchange, userinfo fetch, raider upsert, session cookie,
        // redirect to app) is exactly what production runs.
        await loginPage.ClickSignInAsync();

        // Wait for the browser to land back on the app after the full OAuth
        // redirect chain. The default post-login redirect is /runs.
        await Assertions.Expect(Page!).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs(\?|$)"),
            new() { Timeout = 30000 });

        // Assert the user is signed in — the Sign Out button is only rendered
        // inside <AuthorizeView><Authorized>, so its presence proves the auth
        // cookie the real callback set is being honoured by the Blazor client.
        var navBar = new NavBar(Page!);
        await Assertions.Expect(navBar.SignOutButton).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task ProductionCallback_TransientMe503AfterCallback_RetriesAndShowsSignedIn()
    {
        var loginPage = new LoginPage(Page!);

        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 30000 });

        var meFailuresInjected = 0;
        await Page!.RouteAsync("**/api/v1/me", async route =>
        {
            if (meFailuresInjected == 0)
            {
                meFailuresInjected++;
                await route.FulfillAsync(new()
                {
                    Status = 503,
                    ContentType = "text/plain",
                    Body = "Service Unavailable"
                });
                return;
            }

            await route.ContinueAsync();
        });

        await loginPage.ClickSignInAsync();
        await Assertions.Expect(Page!).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs(\?|$)"),
            new() { Timeout = 30000 });

        var navBar = new NavBar(Page!);
        await Assertions.Expect(navBar.SignOutButton).ToBeVisibleAsync(new() { Timeout = 15000 });
        Assert.Equal(1, meFailuresInjected);
    }
}
