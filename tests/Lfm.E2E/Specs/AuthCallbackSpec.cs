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
// OAuth Testcontainer:
//   1. Browser navigates to /login and clicks Sign in with Battle.net
//   2. API's BattleNetLoginFunction redirects to the NAV mock-oauth2-server
//      authorize endpoint
//   3. The test submits the mock provider login form, and the provider returns
//      a 302 back to the API callback with a fake code and the state echoed
//      from the authorize request
//   4. API's BattleNetCallbackFunction exchanges the code for a token
//      (Testcontainer token endpoint), fetches the user (Testcontainer
//      userinfo endpoint),
//      upserts the raider, sets the auth cookie, and redirects to the app
//   5. Browser lands on the authenticated home page
//
// The test asserts the end of the flow (signed-in navbar on /runs) — the
// assertion target that proves the contract users actually care about.
[Collection("AuthCallback")]
[Trait("Category", E2ELanes.AuthFlow)]
public class AuthCallbackSpec(AuthCallbackFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    // See AuthSpec for the rationale on the WASM patterns — this spec
    // exercises the same cold-start Blazor bundle download path.
    protected override string[] IgnoredConsolePatterns =>
        ["401", "503", "/api/v1/me", "MONO_WASM", ".wasm", "mono_download_assets"];

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

    // E2E scope: proves the real callback path signs in and lands the browser on runs.
    // Cheaper lanes cannot prove this because OAuth redirects, callback cookies, and SPA auth state compose here.
    // Shared data: disposable.
    [Fact]
    public async Task ProductionCallback_StubbedOAuthProvider_SignsInAndLandsOnRuns()
    {
        await AuthHelper.AuthenticateThroughOAuthAsync(
            Page!,
            fixture.Stack.AppBaseUrl,
            redirect: "/runs");

        // Assert the user is signed in — the Sign Out button is only rendered
        // inside <AuthorizeView><Authorized>, so its presence proves the auth
        // cookie the real callback set is being honoured by the Blazor client.
        var navBar = new NavBar(Page!);
        await Assertions.Expect(navBar.AccountMenuButton).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    // E2E scope: proves the browser recovers from a transient post-callback /me failure.
    // Cheaper lanes cannot prove this because retry behavior must preserve the callback session in the SPA.
    // Shared data: disposable.
    [Fact]
    public async Task ProductionCallback_TransientMe503AfterCallback_RetriesAndShowsSignedIn()
    {
        var meFailuresInjected = 0;

        await AuthHelper.AuthenticateThroughOAuthAsync(
            Page!,
            fixture.Stack.AppBaseUrl,
            redirect: "/runs",
            beforeSignInClick: async page =>
            {
                await page.RouteAsync("**/api/v1/me", async route =>
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
            });

        var navBar = new NavBar(Page!);
        await Assertions.Expect(navBar.AccountMenuButton).ToBeVisibleAsync(new() { Timeout = 15000 });
        Assert.Equal(1, meFailuresInjected);
    }
}
