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

[Collection("Profile")]
[Trait("Category", E2ELanes.Functional)]
public class ProfileSpec(ProfileFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    // The seeded raider has `accountProfileRefreshedAt = now`, so the refresh
    // endpoint returns 429 on the RefreshCharacters_Click test. That test's
    // intent is to verify the button is wired to the correct route, not that
    // the refresh actually completes — ignore the 429 from the refresh path.
    // The browser console only carries the status code in <c>msg.Text</c>
    // (not the URL), so we filter on the literal "status of 429" substring.
    // The 401 / /api/v1/me pattern is the default for all specs.
    protected override string[] IgnoredConsolePatterns =>
        ["401", "/api/v1/me", "status of 429"];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
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

    // -------------------------------------------------------------------------
    // Characters tests (4.2)
    // -------------------------------------------------------------------------

    // E2E scope: proves the characters page renders the seeded account characters in the browser.
    // Cheaper lanes cannot prove this because auth, API data, and component rendering must compose in the SPA.
    // Shared data: read-only.
    [Fact]
    public async Task CharactersPage_Loads_DisplaysCharacterList()
    {
        var charactersPage = new CharactersPage(Page!);

        // Block the portrait request — it's fire-and-forget and crashes Blazor
        // when the E2E API can't process it (known app issue).
        await Page!.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"portraits\":{}}",
            });
        });

        await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // DefaultSeed populates accountProfileSummary.wowAccounts[0].characters
        // with exactly two characters (Aelrin + Aelrinalt); the characters endpoint
        // must return both and the page must render one card per character.
        await Assertions.Expect(charactersPage.CharacterList)
            .ToHaveCountAsync(2, new() { Timeout = 15000 });
        await Assertions.Expect(charactersPage.CharacterCard("Aelrin"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(charactersPage.CharacterCard("Aelrinalt"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // RefreshCharactersButton_DispatchesRefreshRequest was removed in favour of
    // CharactersPage_RefreshButton_Click_Replaces_Character_List in
    // tests/Lfm.App.Tests/CharactersPagesTests.cs. The bUnit test proves the
    // user-observable outcome (the rendered card list changes) without needing
    // a full stack bringup or the E2E-only 429 cooldown dance. The refresh
    // endpoint's own contract is covered by BattleNetCharactersRefreshFunctionTests.

    // -------------------------------------------------------------------------
    // Guild tests (4.3)
    // -------------------------------------------------------------------------

    // E2E scope: proves the guild page renders seeded guild information in the browser.
    // Cheaper lanes cannot prove this because auth-scoped routing, API data, and UI rendering must compose.
    // Shared data: read-only.
    [Fact]
    public async Task GuildPage_Loads_DisplaysGuildInfo()
    {
        var guildPage = new GuildPage(Page!);

        await guildPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(guildPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The seeded guild document has blizzardProfileRaw with name "Test Guild".
        await Assertions.Expect(guildPage.GuildNameHeading).ToBeVisibleAsync(new() { Timeout = 10000 });

        Log("Guild page rendered with guild info visible");
    }

    // E2E scope: proves the guild admin settings form renders for the authenticated browser user.
    // Cheaper lanes cannot prove this because authorization, route hydration, and Fluent form rendering compose here.
    // Shared data: read-only.
    [Fact]
    public async Task GuildAdmin_Loads_DisplaysSettings()
    {
        await AuthenticateSiteAdminAsync();
        var guildAdminPage = new GuildAdminPage(Page!);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);

        await Assertions.Expect(guildAdminPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(guildAdminPage.GuildName("Test Guild")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // The settings form is rendered when guild data loads successfully.
        await Assertions.Expect(guildAdminPage.OverrideSettingsHeading)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await Assertions.Expect(guildAdminPage.SaveButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        Log("Guild admin settings form is visible");
    }

    // E2E scope: proves a production-like guild with a fresh roster but no
    // stored rankPermissions still renders permission configuration controls.
    // Cheaper lanes prove the default permission values; the browser journey
    // proves the admin route receives usable controls instead of hiding the
    // whole Rank Permissions section.
    // Shared data: read-only.
    [Fact]
    public async Task GuildAdmin_UnconfiguredRankPermissions_DisplaysDefaultPermissionControls()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId: DefaultSeed.SiteAdminBattleNetId,
            redirect: "/guild/admin");
        var page = await authContext.NewPageAsync();

        var guildAdminPage = new GuildAdminPage(page);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await guildAdminPage.LoadGuildAsync("67890");

        await Assertions.Expect(guildAdminPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(guildAdminPage.RankPermissionsHeading)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(guildAdminPage.RankLabel(0)).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(guildAdminPage.RankLabel(5)).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves SiteAdmin authorization, typed guild-id load controls,
    // and admin API hydration compose in the real browser. bUnit already proves
    // the GetAdminAsync call shape; this verifies the protected page can load a
    // second seeded guild by id after one guild is already rendered.
    // Shared data: read-only.
    [Fact]
    public async Task GuildAdmin_SiteAdmin_LoadsAnotherGuildById()
    {
        await AuthenticateSiteAdminAsync();
        var guildAdminPage = new GuildAdminPage(Page!);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
        await Assertions.Expect(guildAdminPage.GuildName("Test Guild"))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await guildAdminPage.LoadGuildAsync("67890");

        await Assertions.Expect(guildAdminPage.GuildName("Unconfigured Guild"))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(guildAdminPage.GuildIdChip("67890"))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(guildAdminPage.RankLabel(5))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves the browser navigation manager is intercepted by the
    // unsaved-changes dialog and both Stay and Leave choices affect real
    // navigation. bUnit covers the guard contract; only E2E proves it with
    // routed browser links and the native dialog module.
    // Shared data: dirty form only, not saved.
    [Fact]
    public async Task GuildAdmin_UnsavedDirtyFormNavigation_StayAndLeaveWork()
    {
        await AuthenticateSiteAdminAsync();
        var guildAdminPage = new GuildAdminPage(Page!);
        var navBar = new NavBar(Page!);
        var runsPage = new RunsPage(Page!);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
        await Assertions.Expect(guildAdminPage.SloganField)
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await guildAdminPage.SloganField.FillAsync($"E2E unsaved {Guid.NewGuid():N}");
        await navBar.RunsLink.ClickAsync();

        await Assertions.Expect(guildAdminPage.UnsavedDialog)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Page!).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/guild/admin$"),
            new() { Timeout = 5000 });

        await guildAdminPage.StayButton.ClickAsync();
        await Assertions.Expect(guildAdminPage.UnsavedDialog)
            .Not.ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(Page!).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/guild/admin$"),
            new() { Timeout = 5000 });

        await navBar.RunsLink.ClickAsync();
        await Assertions.Expect(guildAdminPage.UnsavedDialog)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await guildAdminPage.LeaveButton.ClickAsync();

        await Assertions.Expect(Page!).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs$"),
            new() { Timeout = 15000 });
        await Assertions.Expect(runsPage.CreateRunButton)
            .ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    // E2E scope: proves guild admin browser edits persist and reload into the form.
    // Cheaper lanes cannot prove this because form binding, API patch, storage, and page reload must round-trip.
    // Shared data: restored.
    [Fact]
    public async Task GuildAdmin_UpdateSettings_ChangesReflected()
    {
        await AuthenticateSiteAdminAsync();
        var guildAdminPage = new GuildAdminPage(Page!);

        // Log API requests to debug 400 errors
        Page!.Request += (_, req) =>
        {
            if (req.Url.Contains("/api/guild") && req.Method is "PATCH")
                Log($"[API REQ] {req.Method} {req.Url} body={req.PostData}");
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Url.Contains("/api/guild") && resp.Status >= 400)
                Log($"[API RESP] {resp.Status} {resp.Url}");
        };

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
        await Assertions.Expect(guildAdminPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(guildAdminPage.SaveButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Snapshot the seeded slogan so the test can restore it — the guild
        // document is shared across the whole suite and mutating its slogan
        // permanently would leak across future runs.
        var originalSlogan = await guildAdminPage.SloganField.InputValueAsync();
        var newSlogan = $"E2E updated slogan {Guid.NewGuid():N}";

        try
        {
            await guildAdminPage.SloganField.FillAsync(newSlogan);
            await guildAdminPage.SaveButton.ClickAsync();

            // Success message should appear confirming the save.
            await Assertions.Expect(guildAdminPage.SuccessMessage).ToBeVisibleAsync(new() { Timeout = 15000 });

            // Re-read: reload the page and verify the persisted slogan round-tripped
            // through Cosmos. The success banner alone proves the API returned 200 —
            // it does not prove the value persisted, which a future regression that
            // swallows the body would silently break.
            await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
            await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
            await Assertions.Expect(guildAdminPage.SloganField).ToBeVisibleAsync(new() { Timeout = 15000 });
            var persistedSlogan = await guildAdminPage.SloganField.InputValueAsync();
            Assert.Equal(newSlogan, persistedSlogan);
        }
        finally
        {
            // Restore the seeded slogan so sibling tests see a clean fixture.
            await guildAdminPage.SloganField.FillAsync(originalSlogan);
            await guildAdminPage.SaveButton.ClickAsync();
            await Assertions.Expect(guildAdminPage.SuccessMessage).ToBeVisibleAsync(new() { Timeout = 15000 });
        }
    }

    private Task AuthenticateSiteAdminAsync() =>
        AuthHelper.AuthenticatePageAsync(
            Page!,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            DefaultSeed.SiteAdminBattleNetId,
            "/guild/admin");

    // -------------------------------------------------------------------------
    // Delete account test (4.4)
    // -------------------------------------------------------------------------

    // E2E scope: proves account deletion redirects the browser to goodbye after confirmation.
    // Cheaper lanes cannot prove this because confirmation UI, session state, deletion, and redirect must compose.
    // Shared data: disposable.
    [Fact]
    public async Task DeleteAccount_Confirm_RedirectsToGoodbye()
    {
        // Use a disposable test user to avoid invalidating the shared primary
        // and secondary users used by other tests.
        var deleteContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId: DefaultSeed.DisposableBattleNetId,
            redirect: "/characters");
        var deletePage = await deleteContext.NewPageAsync();

        try
        {
            var charactersPage = new CharactersPage(deletePage);

            // Stub the portrait endpoint to prevent fire-and-forget crash
            await deletePage.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
            {
                await route.FulfillAsync(new()
                {
                    Status = 200,
                    ContentType = "application/json",
                    Body = "{\"portraits\":{}}",
                });
            });

            await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);
            await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Assertions.Expect(charactersPage.DeleteConfirmationField)
                .ToBeVisibleAsync(new() { Timeout = 10000 });
            await charactersPage.DeleteConfirmationField.FillAsync("FORGET ME");
            // Tab out to trigger blur/change event for Blazor binding
            await deletePage.Keyboard.PressAsync("Tab");

            await Assertions.Expect(charactersPage.DeleteAccountButton)
                .ToBeEnabledAsync(new() { Timeout = 5000 });
            await charactersPage.DeleteAccountButton.ClickAsync();

            // Should redirect to /goodbye after successful deletion.
            await Assertions.Expect(deletePage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/goodbye"),
                new() { Timeout = 15000 });

            await Assertions.Expect(deletePage.GetByText("Goodbye")).ToBeVisibleAsync(
                new() { Timeout = 10000 });

            Log("Delete account flow completed — redirected to /goodbye");
        }
        finally
        {
            await deleteContext.CloseAsync();
        }
    }
}
