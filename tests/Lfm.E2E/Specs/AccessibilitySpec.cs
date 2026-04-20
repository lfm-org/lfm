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

[Collection("Accessibility")]
[Trait("Category", "Accessibility")]
public class AccessibilitySpec(AccessibilityFixture fixture, ITestOutputHelper output)
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

    // -------------------------------------------------------------------------
    // Public route axe-core scans — no auth required
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LandingPage_MeetsWcag22AA()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the sign-in button to confirm the page has rendered
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(Page, Output, "/ (landing)");
    }

    [Fact]
    public async Task LoginPage_MeetsWcag22AA()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/login",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var loginPage = new LoginPage(Page);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(Page, Output, "/login (load)");

        // Re-scan after keyboard-focusing the sign-in button. The focus
        // indicator styles, any aria-describedby tooltips, and the focused
        // element's contrast against its focus ring only surface after
        // interaction — a load-time scan alone misses those (`E-HC-A2`).
        await AccessibilityHelper.ScanAfterAsync(Page, Output, "/login (sign-in focused)", async () =>
        {
            await loginPage.SignInButton.FocusAsync();
        });
    }

    [Fact]
    public async Task PrivacyPage_MeetsWcag22AA()
    {
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/privacy",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Privacy Policy heading to confirm the page has rendered
        await Assertions.Expect(
            Page.GetByRole(AriaRole.Heading, new() { Name = "Privacy Policy" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(Page, Output, "/privacy");
    }

    [Fact]
    public async Task LoginFailedPage_MeetsWcag22AA()
    {
        var loginFailedPage = new LoginFailedPage(Page!);

        await loginFailedPage.GotoAsync(fixture.Stack.AppBaseUrl);

        // WASM runtime may not be cached yet on this route — allow extra time
        await Assertions.Expect(loginFailedPage.ErrorHeading).ToBeVisibleAsync(new() { Timeout = 30000 });

        await AccessibilityHelper.ScanAndAssert(Page!, Output, "/login/failed");
    }

    // -------------------------------------------------------------------------
    // Authenticated route axe-core scans
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunsPage_MeetsWcag22AA()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Runs heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "Runs" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/runs (load)");

        // Re-scan after surfacing the detail panel — this is the dynamic content
        // surface that load-time scans miss (`E-HC-A2`). Selecting a run swaps in
        // the role-column roster, the Edit button, and the attendance sections.
        await AccessibilityHelper.ScanAfterAsync(page, Output, "/runs (run selected)", async () =>
        {
            var runsPage = new RunsPage(page);
            await runsPage.SelectRunAsync(DefaultSeed.TestRunId);
            await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        });
    }

    [Fact]
    public async Task RunDetailPage_MeetsWcag22AA()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();
        var runUrl = $"{fixture.Stack.AppBaseUrl}/runs/{Uri.EscapeDataString(DefaultSeed.TestRunId)}";

        await page.GotoAsync(runUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Runs heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "Runs" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, $"/runs/{DefaultSeed.TestRunId}");
    }

    [Fact]
    public async Task CharactersPage_MeetsWcag22AA()
    {
        // Stub the portrait endpoint so we don't trip the fire-and-forget crash
        // documented in CharactersPage_Loads_DisplaysCharacterList.
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();
        await page.RouteAsync("**/api/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"portraits\":{}}",
            });
        });

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the My Characters heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "My Characters" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/characters (load)");

        // Re-scan after dirtying the delete-confirmation field. Typing into the
        // FluentUI text-field surfaces the disabled→enabled state of the delete
        // button — a transition that load-time scans miss (`E-HC-A2`).
        await AccessibilityHelper.ScanAfterAsync(page, Output, "/characters (delete field dirty)", async () =>
        {
            var deleteField = page.Locator("fluent-text-field[placeholder='FORGET ME'] input");
            await Assertions.Expect(deleteField).ToBeVisibleAsync(new() { Timeout = 10000 });
            await deleteField.ClickAsync();
            await deleteField.PressSequentiallyAsync("FORG");
            await page.Keyboard.PressAsync("Tab");
        });
    }

    [Fact]
    public async Task GuildPage_MeetsWcag22AA()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/guild",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Guild page heading (use typo attribute to disambiguate from nav)
        await Assertions.Expect(
            page.Locator("[typo='h1']").Filter(new() { HasTextString = "Guild" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/guild");
    }

    [Fact]
    public async Task GuildAdminPage_MeetsWcag22AA()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/guild/admin",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Guild Admin heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "Guild Admin" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/guild/admin (load)");

        // Re-scan after dirtying the slogan field. The form's dirty state, the
        // Save button's enabled transition, and any inline validation hints all
        // live behind interaction (`E-HC-A2`).
        await AccessibilityHelper.ScanAfterAsync(page, Output, "/guild/admin (slogan dirty)", async () =>
        {
            var guildAdminPage = new GuildAdminPage(page);
            await Assertions.Expect(guildAdminPage.SloganField).ToBeVisibleAsync(new() { Timeout = 10000 });
            await guildAdminPage.SloganField.FillAsync($"E2E a11y dirty {Guid.NewGuid():N}");
            await page.Keyboard.PressAsync("Tab");
        });
    }

    [Fact]
    public async Task InstancesPage_MeetsWcag22AA()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/instances",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the Instances heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "Instances" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/instances");
    }

    // -------------------------------------------------------------------------
    // Keyboard navigation tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TabFromBody_FirstStopIsSkipToContentLink()
    {
        // WCAG 2.4.1 (Bypass Blocks): the skip-to-content link must be the
        // FIRST tab stop on every page so keyboard users can jump past the
        // navbar. The link is declared at the top of MainLayout.razor.
        // Walking the actual sequence and asserting position 1 catches both
        // a missing skip link AND any element accidentally inserted before it
        // (toolbar, banner ad, etc.) that would push it further down the
        // tab order. The previous version only asserted that focus moved off
        // <body>, which any element on the page would satisfy — that is a
        // characterization smell (`E-HC-F1`).
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        await Page!.Keyboard.PressAsync("Tab");

        var firstTag = await Page.EvaluateAsync<string>(
            "() => document.activeElement?.tagName?.toUpperCase() ?? ''");
        var firstText = await Page.EvaluateAsync<string>(
            "() => (document.activeElement?.textContent ?? '').trim()");

        Assert.Equal("A", firstTag);
        Assert.Equal("Skip to content", firstText);
    }

    [Fact]
    public async Task InteractiveElements_ReachableViaTab()
    {
        // WCAG 2.1.1 (Keyboard): interactive controls must be reachable via Tab.
        // Asserting only Assert.NotEmpty(focusedTags) passes for any tab stop —
        // the skip link alone would satisfy it — and a regression that made the
        // sign-in button unreachable would go undetected. Walk the tab sequence
        // and assert the sign-in button is actually one of the stops (`E-HC-F1`).
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Collect (tag, text) for each focusable tab stop, up to 20 stops.
        var stops = new List<(string Tag, string Text)>();
        for (var i = 0; i < 20; i++)
        {
            await Page!.Keyboard.PressAsync("Tab");
            var tag = await Page.EvaluateAsync<string>(
                "() => document.activeElement?.tagName?.toUpperCase() ?? ''");
            if (string.IsNullOrEmpty(tag) || tag == "BODY") break;
            var text = await Page.EvaluateAsync<string>(
                "() => (document.activeElement?.textContent ?? '').trim()");
            stops.Add((tag, text));
        }

        Assert.Contains(stops, s => s.Text.Contains("Sign in with Battle.net"));
    }

    [Fact]
    public async Task FocusIndicator_VisibleOnAllTabbableElements()
    {
        // WCAG 2.4.7 (Focus Visible): every keyboard-reachable element must
        // show a visible focus indicator, not just the first. The previous
        // version only pressed Tab once and inspected a single stop, which
        // passed even if the skip link had an outline but every subsequent
        // tabstop lost its focus style in a later regression (`E-HC-F4`).
        // Iterate the full tab sequence and assert each stop has a visible
        // outline or box-shadow.
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        var checkedStops = 0;
        for (var i = 0; i < 20; i++)
        {
            await Page!.Keyboard.PressAsync("Tab");

            var tag = await Page.EvaluateAsync<string>(
                "() => document.activeElement?.tagName?.toUpperCase() ?? ''");
            if (string.IsNullOrEmpty(tag) || tag == "BODY") break;

            var text = await Page.EvaluateAsync<string>(
                "() => (document.activeElement?.textContent ?? '').trim().substring(0, 60)");
            var hasFocusStyle = await Page.EvaluateAsync<bool>(@"() => {
                const el = document.activeElement;
                if (!el) return false;
                const style = window.getComputedStyle(el);
                const hasOutline = style.outline !== 'none' && style.outlineWidth !== '0px';
                const hasShadow = style.boxShadow && style.boxShadow !== 'none';
                return hasOutline || hasShadow;
            }");

            Assert.True(hasFocusStyle,
                $"Tab stop {i + 1} (<{tag}> \"{text}\") has no visible focus indicator");
            checkedStops++;
        }

        // Must have reached at least the skip link + sign-in button.
        Assert.True(checkedStops >= 2,
            $"Expected at least 2 focus-checkable tab stops, walked {checkedStops}");
    }

    // -------------------------------------------------------------------------
    // Keyboard activation tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SignInButton_ActivatesWithEnter()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Register request listener before triggering navigation
        var loginRequestTask = Page!.WaitForRequestAsync(
            new System.Text.RegularExpressions.Regex(@"/api/battlenet/login"),
            new() { Timeout = 10000 });

        await loginPage.SignInButton.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");

        // Pressing Enter on the sign-in button should initiate the OAuth request
        await loginRequestTask;

        // Park the page on about:blank so the disposal-time console-error
        // assertion does not pick up the 404 cascade for the offline external
        // Battle.net assets the browser tried to load after the redirect.
        await Page.GotoAsync("about:blank");
    }

    [Fact]
    public async Task SignInButton_ActivatesWithSpace()
    {
        // Use a fresh context so the request task doesn't fire from a previous test
        await using var freshContext = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        var freshPage = await freshContext.NewPageAsync();

        var loginPage = new LoginPage(freshPage);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        var loginRequestTask = freshPage.WaitForRequestAsync(
            new System.Text.RegularExpressions.Regex(@"/api/battlenet/login"),
            new() { Timeout = 10000 });

        await loginPage.SignInButton.FocusAsync();
        await freshPage.Keyboard.PressAsync("Space");

        await loginRequestTask;
    }

    [Fact]
    public async Task NavLinks_ActivateWithEnter()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var navBar = new NavBar(page);
        await Assertions.Expect(navBar.SignOutButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Find the Characters nav link and activate it with Enter
        var charactersLink = navBar.CharactersLink;
        await Assertions.Expect(charactersLink).ToBeVisibleAsync(new() { Timeout = 10000 });

        await charactersLink.FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/characters"),
            new() { Timeout = 15000 });
    }

    [Fact]
    public async Task FormSubmission_WorksWithEnter()
    {
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the characters page to load
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "My Characters" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        // Find the delete confirmation text field, type partial text and press Enter.
        // The button is disabled when confirmation != "FORGET ME", so pressing Enter
        // in the field should not navigate away.
        // Target the inner input of the FluentTextField component
        var deleteField = page.Locator("fluent-text-field[placeholder='FORGET ME'] input");
        await Assertions.Expect(deleteField).ToBeVisibleAsync(new() { Timeout = 10000 });

        await deleteField.ClickAsync();
        await deleteField.PressSequentiallyAsync("test");
        await page.Keyboard.PressAsync("Enter");

        // Page should still show the characters heading (not navigated away)
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "My Characters" }))
            .ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // -------------------------------------------------------------------------
    // Modal / dialog tests
    // Note: No modal dialogs exist in the current Blazor app. Blazor WASM uses
    // FluentUI components and inline state instead of overlay dialogs. These
    // tests are therefore omitted. If modals are introduced in future, add:
    //   ModalDialogs_TrapFocus and ModalDialogs_EscapeCloses here.
    // -------------------------------------------------------------------------
}
