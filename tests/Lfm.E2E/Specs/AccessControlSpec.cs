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

[Collection("AccessControl")]
[Trait("Category", E2ELanes.Functional)]
public class AccessControlSpec(AccessControlFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    protected override string[] IgnoredConsolePatterns => ["401", "/api/me"];

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

    // E2E scope: proves unauthenticated browser navigation redirects protected routes to login.
    // Cheaper lanes cannot prove this because the SPA auth-state check and client redirect run in the browser.
    // Shared data: none.
    [Theory]
    [InlineData("/runs", "%2Fruns")]
    [InlineData("/characters", "%2Fcharacters")]
    [InlineData("/runs/new", "%2Fruns%2Fnew")]
    [InlineData("/guild", "%2Fguild")]
    [InlineData("/guild/admin", "%2Fguild%2Fadmin")]
    [InlineData("/instances", "%2Finstances")]
    public async Task ProtectedRoute_Unauthenticated_RedirectsToLogin(string route, string expectedRedirectParam)
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}{route}",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex($@"/login\?redirect={expectedRedirectParam}$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves an authenticated browser session can render the protected runs route.
    // Cheaper lanes cannot prove this because cookie-backed SPA authorization is browser state.
    // Shared data: read-only.
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
            await Assertions.Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/runs$"));

            // Verify authenticated nav is visible — confirms auth state
            var navBar = new NavBar(authPage);
            await Assertions.Expect(navBar.SignOutButton).ToBeVisibleAsync();
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    // E2E scope: proves the public landing page renders without auth redirection.
    // Cheaper lanes cannot prove this because the absence of client redirect is browser routing behavior.
    // Shared data: none.
    [Fact]
    public async Task PublicLandingPage_Unauthenticated_RendersWithoutRedirect()
    {
        var landingPage = new LandingPage(Page!);
        await landingPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(landingPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.DoesNotContain("/login?redirect", Page!.Url);
    }

    // E2E scope: proves the public login page renders without adding a redirect parameter.
    // Cheaper lanes cannot prove this because browser URL state and SPA routing must both settle.
    // Shared data: none.
    [Fact]
    public async Task PublicLoginPage_Unauthenticated_RendersWithoutRedirect()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.Contains("/login", Page!.Url);
        Assert.DoesNotContain("redirect=", Page.Url);
    }

    // E2E scope: proves the public privacy page renders without auth redirection.
    // Cheaper lanes cannot prove this because the browser observes the final routed URL and page.
    // Shared data: none.
    [Fact]
    public async Task PublicPrivacyPage_Unauthenticated_RendersWithoutRedirect()
    {
        var privacyPage = new PrivacyPage(Page!);
        await privacyPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(privacyPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.Contains("/privacy", Page!.Url);
        Assert.DoesNotContain("/login?redirect", Page.Url);
    }
}
