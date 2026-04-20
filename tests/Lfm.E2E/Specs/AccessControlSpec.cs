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
[Trait("Category", "Functional")]
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

    [Fact]
    public async Task PublicLandingPage_Unauthenticated_RendersWithoutRedirect()
    {
        var landingPage = new LandingPage(Page!);
        await landingPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(landingPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.DoesNotContain("/login?redirect", Page!.Url);
    }

    [Fact]
    public async Task PublicLoginPage_Unauthenticated_RendersWithoutRedirect()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Assert.Contains("/login", Page!.Url);
        Assert.DoesNotContain("redirect=", Page.Url);
    }

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
