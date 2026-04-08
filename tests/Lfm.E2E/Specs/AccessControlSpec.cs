using FluentAssertions;
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
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Context is not null)
            await Context.CloseAsync();
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromRuns()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromCharacters()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fcharacters$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromCreateRun()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs/new",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns%2Fnew$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromGuild()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/guild",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fguild$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromGuildAdmin()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/guild-admin",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fguild-admin$"),
            new() { Timeout = 30000 });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task ProtectedRoute_Unauthenticated_RedirectsFromInstances()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/instances",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Finstances$"),
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
    public async Task PublicRoute_Unauthenticated_RendersWithoutRedirect()
    {
        // Landing page
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        Page.Url.Should().NotContain("/login?redirect");

        // Login page
        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/login",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        Page.Url.Should().Contain("/login");
        Page.Url.Should().NotContain("redirect=");

        // Privacy page
        await Page.GotoAsync($"{fixture.Stack.AppBaseUrl}/privacy",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        Page.Url.Should().Contain("/privacy");
        Page.Url.Should().NotContain("/login?redirect");
    }
}
