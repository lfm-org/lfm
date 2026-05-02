// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("LayoutIntegrity")]
[Trait("Category", E2ELanes.LayoutIntegrity)]
public sealed class LayoutIntegritySpec(LayoutIntegrityFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    private static readonly LayoutViewport[] Viewports =
    [
        new(320, 568, "mobile-floor"),
        new(375, 667, "narrow-phone"),
        new(767, 900, "below-tablet-breakpoint"),
        new(768, 900, "tablet-breakpoint"),
        new(1023, 768, "below-wide-breakpoint"),
        new(1024, 768, "wide-breakpoint"),
        new(1280, 720, "desktop")
    ];

    protected override string[] IgnoredConsolePatterns => ["401", "/api/v1/me"];

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

    // E2E scope: proves public pages have no browser-computed element overlap
    // or document-level horizontal overflow at the responsive breakpoint edges.
    [Fact]
    public async Task PublicRoutes_HaveNoLayoutOverlaps()
    {
        foreach (var viewport in Viewports)
        {
            await ScanPublicRouteAsync(viewport, "/", async page =>
            {
                await Assertions.Expect(page.GetByRole(
                    AriaRole.Button,
                    new() { Name = "Sign in with Battle.net" }))
                    .ToBeVisibleAsync(new() { Timeout = 15000 });
            });

            await ScanPublicRouteAsync(viewport, "/login", async page =>
            {
                await Assertions.Expect(page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Sign In" }))
                    .ToBeVisibleAsync(new() { Timeout = 15000 });
            });

            await ScanPublicRouteAsync(viewport, "/privacy", async page =>
            {
                await Assertions.Expect(page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Privacy Policy" }))
                    .ToBeVisibleAsync(new() { Timeout = 15000 });
            });

            await ScanPublicRouteAsync(viewport, "/auth/failure", async page =>
            {
                var failedPage = new LoginFailedPage(page);
                await Assertions.Expect(failedPage.ErrorHeading).ToBeVisibleAsync(new() { Timeout = 30000 });
            });
        }
    }

    // E2E scope: proves authenticated dense pages and dynamic states have no
    // browser-computed element overlap or document-level horizontal overflow.
    [Fact]
    public async Task AuthenticatedDenseRoutes_HaveNoLayoutOverlaps()
    {
        var page = Page!;
        await StubPortraitsAsync(page);
        await AuthHelper.AuthenticatePageAsync(page, fixture.Stack.ApiBaseUrl, fixture.Stack.AppBaseUrl);

        foreach (var viewport in Viewports)
        {
            await ScanRunsSelectedAsync(viewport);
            await ScanCreateRunDungeonAsync(viewport);
            await ScanCharactersAsync(viewport);
            await ScanGuildAdminDirtyAsync(viewport);
            await ScanInstancesAsync(viewport);
        }
    }

    // E2E scope: proves text expansion in the supported Finnish locale does
    // not introduce mobile-floor overlap in the densest authenticated states.
    [Fact]
    public async Task FinnishDenseStates_HaveNoMobileLayoutOverlaps()
    {
        await ResetContextAsync(new()
        {
            Locale = "fi-FI",
            ViewportSize = new() { Width = 320, Height = 568 },
        });

        var page = Page!;
        await StubPortraitsAsync(page);
        await AuthHelper.AuthenticatePageAsync(page, fixture.Stack.ApiBaseUrl, fixture.Stack.AppBaseUrl);

        await ScanRunsSelectedAsync(new(320, 568, "mobile-floor-fi"));
        await ScanCreateRunDungeonAsync(new(320, 568, "mobile-floor-fi"));
        await ScanCharactersAsync(new(320, 568, "mobile-floor-fi"));
        await ScanGuildAdminDirtyAsync(new(320, 568, "mobile-floor-fi"));
    }

    private async Task ScanPublicRouteAsync(
        LayoutViewport viewport,
        string route,
        Func<IPage, Task> waitForReady)
    {
        await SetViewportAsync(viewport);
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}{route}", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await waitForReady(Page);
        await ScanCurrentPageAsync($"{route} ({viewport.Name})");
    }

    private async Task ScanRunsSelectedAsync(LayoutViewport viewport)
    {
        await SetViewportAsync(viewport);
        var page = Page!;
        var runsPage = new RunsPage(page);
        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new("Runs|Runit") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await runsPage.SelectRunAsync(DefaultSeed.TestRunId);
        await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await ScanCurrentPageAsync($"/runs selected ({viewport.Name})");
    }

    private async Task ScanCreateRunDungeonAsync(LayoutViewport viewport)
    {
        await SetViewportAsync(viewport);
        var page = Page!;
        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs/new", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new("Schedule a run|Uusi runi") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await page.GetByRole(AriaRole.Radio, new() { NameRegex = new("Dungeon|Luolasto") }).ClickAsync();
        await ScanCurrentPageAsync($"/runs/new dungeon ({viewport.Name})");
    }

    private async Task ScanCharactersAsync(LayoutViewport viewport)
    {
        await SetViewportAsync(viewport);
        var page = Page!;
        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new("My Characters|Hahmoni") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await ScanCurrentPageAsync($"/characters ({viewport.Name})");
    }

    private async Task ScanGuildAdminDirtyAsync(LayoutViewport viewport)
    {
        await SetViewportAsync(viewport);
        var page = Page!;
        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/guild/admin", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new("Guild Admin|Killan hallinta") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        var guildAdminPage = new GuildAdminPage(page);
        await Assertions.Expect(guildAdminPage.SloganField).ToBeVisibleAsync(new() { Timeout = 10000 });
        await guildAdminPage.SloganField.FillAsync($"E2E layout dirty {Guid.NewGuid():N}");
        await page.Keyboard.PressAsync("Tab");
        await ScanCurrentPageAsync($"/guild/admin dirty ({viewport.Name})");
    }

    private async Task ScanInstancesAsync(LayoutViewport viewport)
    {
        await SetViewportAsync(viewport);
        var page = Page!;
        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/instances", new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new("Instances|Instanssit") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await ScanCurrentPageAsync($"/instances ({viewport.Name})");
    }

    private async Task ResetContextAsync(BrowserNewContextOptions options)
    {
        if (Context is not null)
            await Context.CloseAsync();

        Context = await fixture.Stack.Browser.NewContextAsync(options);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
        await StartTracingAsync();
    }

    private async Task SetViewportAsync(LayoutViewport viewport)
    {
        await Page!.SetViewportSizeAsync(viewport.Width, viewport.Height);
    }

    private async Task ScanCurrentPageAsync(string context)
    {
        await WaitForLayoutReadyAsync(Page!);
        await LayoutIntegrityHelper.AssertNoOverlapsAsync(Page!, Output, context);
    }

    private static async Task StubPortraitsAsync(IPage page)
    {
        await page.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"portraits\":{}}",
            });
        });
    }

    private static async Task WaitForLayoutReadyAsync(IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.EvaluateAsync("() => document.fonts ? document.fonts.ready : Promise.resolve()");
    }

    private readonly record struct LayoutViewport(int Width, int Height, string Name);
}
