using FluentAssertions;
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

        await AccessibilityHelper.ScanAndAssert(Page, Output, "/login");
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

        await Assertions.Expect(loginFailedPage.ErrorHeading).ToBeVisibleAsync(new() { Timeout = 15000 });

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

        await AccessibilityHelper.ScanAndAssert(page, Output, "/runs");
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
        await using var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);

        var page = await authContext.NewPageAsync();

        await page.GotoAsync($"{fixture.Stack.AppBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the My Characters heading to confirm the page has rendered
        await Assertions.Expect(
            page.GetByRole(AriaRole.Heading, new() { Name = "My Characters" }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        await AccessibilityHelper.ScanAndAssert(page, Output, "/characters");
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
            page.Locator("[typo='h3']").Filter(new() { HasTextString = "Guild" }))
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

        await AccessibilityHelper.ScanAndAssert(page, Output, "/guild/admin");
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
    public async Task TabOrder_FollowsLogicalFlow()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Press Tab from the body and collect the focused element tag
        await Page!.Keyboard.PressAsync("Tab");
        var firstFocus = await Page.EvaluateAsync<string>(
            "() => document.activeElement?.tagName ?? ''");

        // Tab should land on an interactive element, not body or html
        firstFocus.Should().NotBeNullOrWhiteSpace(
            "Tab from body should move focus to the first interactive element");
        firstFocus.ToUpperInvariant().Should().NotBe("BODY",
            "Tab from body should move focus off the body element");
    }

    [Fact]
    public async Task InteractiveElements_ReachableViaTab()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Collect all elements reachable by Tab (up to 20 tabs)
        var focusedTags = new List<string>();
        for (var i = 0; i < 20; i++)
        {
            await Page!.Keyboard.PressAsync("Tab");
            var tag = await Page.EvaluateAsync<string>(
                "() => document.activeElement?.tagName?.toUpperCase() ?? ''");
            if (string.IsNullOrEmpty(tag) || tag == "BODY") break;
            focusedTags.Add(tag);
        }

        // The login page must have at least the sign-in button reachable via Tab
        focusedTags.Should().NotBeEmpty(
            "the login page must have at least one Tab-reachable interactive element");
    }

    [Fact]
    public async Task FocusIndicator_VisibleOnFocusedElements()
    {
        var loginPage = new LoginPage(Page!);
        await loginPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(loginPage.SignInButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Tab to focus the first interactive element
        await Page!.Keyboard.PressAsync("Tab");

        // Check that the focused element has a visible outline or box-shadow
        var hasFocusStyle = await Page.EvaluateAsync<bool>(@"() => {
            const el = document.activeElement;
            if (!el) return false;
            const style = window.getComputedStyle(el);
            const outline = style.outline;
            const boxShadow = style.boxShadow;
            const outlineWidth = style.outlineWidth;
            // Consider visible if outline has non-zero width or box-shadow is set
            const hasOutline = outline !== 'none' && outlineWidth !== '0px';
            const hasShadow = boxShadow && boxShadow !== 'none';
            return hasOutline || hasShadow;
        }");

        hasFocusStyle.Should().BeTrue(
            "focused elements must have a visible focus indicator (outline or box-shadow)");
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
        // Target the inner input of the FluentTextField wrapper
        var deleteField = page.Locator("fluent-text-field[placeholder='FORGET ME'] input");
        await Assertions.Expect(deleteField).ToBeVisibleAsync(new() { Timeout = 10000 });

        await deleteField.FocusAsync();
        await deleteField.FillAsync("test");
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
