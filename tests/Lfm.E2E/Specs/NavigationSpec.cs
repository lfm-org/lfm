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

[Collection("Navigation")]
[Trait("Category", "Functional")]
public class NavigationSpec(NavigationFixture fixture, ITestOutputHelper output)
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

    [Fact]
    public async Task LandingPage_Loads_ShowsFeatureCardsAndCTA()
    {
        var landingPage = new LandingPage(Page!);

        await landingPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(landingPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(landingPage.SharedScheduleCard).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(landingPage.RoleCoverageCard).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(landingPage.BattleNetSignInCard).ToBeVisibleAsync(new() { Timeout = 10000 });
        var ctaVisible = await landingPage.IsSignInButtonVisibleAsync();
        Assert.True(ctaVisible);
    }

    [Fact]
    public async Task InstancesPage_Loads_DisplaysRaidInstances()
    {
        var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var authPage = await authContext.NewPageAsync();

        try
        {
            var instancesPage = new InstancesPage(authPage);
            await instancesPage.GotoAsync(fixture.Stack.AppBaseUrl);

            await Assertions.Expect(instancesPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

            // DefaultSeed seeds one instance ("Liberation of Undermine"); the test
            // name promises a data display, so assert the grid actually rendered it
            // instead of only pinning the page heading.
            await Assertions.Expect(instancesPage.InstanceRows)
                .ToHaveCountAsync(1, new() { Timeout = 15000 });
            await Assertions.Expect(authPage.GetByText("Liberation of Undermine"))
                .ToBeVisibleAsync(new() { Timeout = 10000 });
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    [Fact]
    public async Task PrivacyPolicy_Loads_RendersContent()
    {
        var privacyPage = new PrivacyPage(Page!);

        await privacyPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(privacyPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(privacyPage.DataControllerSection).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task NotFound_UnknownRoute_Shows404()
    {
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/nonexistent-route-that-does-not-exist",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var notFoundPage = new NotFoundPage(Page);

        await Assertions.Expect(notFoundPage.Heading).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(notFoundPage.Message).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task NavbarCharactersLink_Click_NavigatesToCharactersPage()
    {
        var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var authPage = await authContext.NewPageAsync();

        try
        {
            await authPage.RouteAsync("**/api/battlenet/character-portraits", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "{\"portraits\":{}}",
                });
            });

            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs");

            var navBar = new NavBar(authPage);
            await Assertions.Expect(navBar.CharactersLink).ToBeVisibleAsync(new() { Timeout = 10000 });

            await navBar.CharactersLink.ClickAsync();

            // Prove the link reached the destination UI, not just the URL.
            var charactersPage = new CharactersPage(authPage);
            await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Assertions.Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/characters$"),
                new() { Timeout = 10000 });
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    [Fact]
    public async Task NavbarGuildLink_Click_NavigatesToGuildPage()
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
            await Assertions.Expect(navBar.GuildLink).ToBeVisibleAsync(new() { Timeout = 10000 });

            await navBar.GuildLink.ClickAsync();

            var guildPage = new GuildPage(authPage);
            await Assertions.Expect(guildPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
            await Assertions.Expect(authPage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/guild$"),
                new() { Timeout = 10000 });
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }
}
