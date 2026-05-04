// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Navigation")]
[Trait("Category", E2ELanes.Functional)]
public class SiteAdminSpec(NavigationFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId: DefaultSeed.SiteAdminBattleNetId,
            redirect: "/runs");
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

    // E2E scope: proves a real browser session receives the SiteAdmin role from
    // /api/v1/me, renders the admin nav affordance, and reaches the protected
    // reference-data page. Cheaper lanes already cover component markup and the
    // POST refresh contract, so this test intentionally does not click Refresh.
    // Shared data: read-only.
    [Fact]
    public async Task SiteAdmin_NavLink_ReachesReferenceDataPage()
    {
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var adminLink = Page.GetByRole(AriaRole.Link, new() { Name = "Admin", Exact = true });
        await Assertions.Expect(adminLink).ToBeVisibleAsync(new() { Timeout = 15000 });

        await adminLink.ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(
            new Regex(@"/admin/reference$"),
            new() { Timeout = 10000 });
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Reference data" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Refresh now" }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }
}
