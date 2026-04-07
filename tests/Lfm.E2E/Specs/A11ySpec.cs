using Deque.AxeCore.Playwright;
using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class A11ySpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _unauthContext = null!;
    private IPage _unauthPage = null!;
    private IBrowserContext _authContext = null!;
    private IPage _authPage = null!;

    public async Task InitializeAsync()
    {
        _unauthContext = await fixture.Browser.NewContextAsync();
        _unauthPage = await _unauthContext.NewPageAsync();

        _authContext = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _authPage = await _authContext.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _unauthContext.CloseAsync();
        await _authContext.CloseAsync();
    }

    // --- Unauthenticated pages ---

    [Fact]
    public async Task Landing_page_is_axe_clean()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/");
        await Expect(_unauthPage.GetByText("Looking For More")).ToBeVisibleAsync();

        var result = await _unauthPage.RunAxe();
        result.Violations.Should().BeEmpty("landing page should have no WCAG violations");
    }

    [Fact]
    public async Task Login_page_is_keyboard_reachable()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/login");
        // Blazor LoginPage has a button "Sign in with Battle.net" (not a link)
        var battleNetButton = _unauthPage.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });

        await Expect(_unauthPage.GetByText("Sign In")).ToBeVisibleAsync();
        await Expect(battleNetButton).ToBeVisibleAsync();

        await TabUntilFocusedAsync(_unauthPage, battleNetButton);
    }

    [Fact]
    public async Task Login_page_is_axe_clean()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/login");
        await Expect(_unauthPage.GetByText("Sign In")).ToBeVisibleAsync();

        var result = await _unauthPage.RunAxe();
        result.Violations.Should().BeEmpty("login page should have no WCAG violations");
    }

    [Fact]
    public async Task Login_failed_page_is_axe_clean()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/login/failed");
        await Expect(_unauthPage.GetByText("Login Failed")).ToBeVisibleAsync();

        var result = await _unauthPage.RunAxe();
        result.Violations.Should().BeEmpty("login failed page should have no WCAG violations");
    }

    [Fact]
    public async Task Goodbye_page_is_axe_clean()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/goodbye");
        await Expect(_unauthPage.GetByText("Goodbye")).ToBeVisibleAsync();

        var result = await _unauthPage.RunAxe();
        result.Violations.Should().BeEmpty("goodbye page should have no WCAG violations");
    }

    [Fact]
    public async Task Privacy_policy_page_is_axe_clean()
    {
        await _unauthPage.GotoAsync(fixture.AppBaseUrl + "/privacy");
        await Expect(_unauthPage.GetByText("Privacy Policy")).ToBeVisibleAsync();

        var result = await _unauthPage.RunAxe();
        result.Violations.Should().BeEmpty("privacy page should have no WCAG violations");
    }

    // --- Authenticated pages ---

    [Fact]
    public async Task Runs_list_is_keyboard_reachable()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/runs");
        var createRunButton = _authPage.GetByRole(AriaRole.Button, new() { Name = "Create Run" });

        await Expect(createRunButton).ToBeVisibleAsync();
        await TabUntilFocusedAsync(_authPage, createRunButton);
    }

    [Fact]
    public async Task Runs_list_is_axe_clean()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/runs");
        await _authPage.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Expect(_authPage.GetByRole(AriaRole.Button, new() { Name = "Create Run" })).ToBeVisibleAsync();

        var result = await _authPage.RunAxe();
        result.Violations.Should().BeEmpty("runs page should have no WCAG violations");
    }

    [Fact]
    public async Task Characters_page_is_axe_clean()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/characters");
        await Expect(_authPage.GetByText("My Characters")).ToBeVisibleAsync();

        var result = await _authPage.RunAxe();
        result.Violations.Should().BeEmpty("characters page should have no WCAG violations");
    }

    [Fact]
    public async Task Guild_page_is_axe_clean()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/guild");
        await _authPage.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Expect(_authPage.GetByText("Guild").First).ToBeVisibleAsync();

        var result = await _authPage.RunAxe();
        result.Violations.Should().BeEmpty("guild page should have no WCAG violations");
    }

    [Fact]
    public async Task Create_run_page_is_axe_clean()
    {
        await _authPage.GotoAsync(fixture.AppBaseUrl + "/runs/new");
        await _authPage.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });
        await Expect(_authPage.GetByText("Create Run").First).ToBeVisibleAsync();

        var result = await _authPage.RunAxe();
        result.Violations.Should().BeEmpty("create run page should have no WCAG violations");
    }

    private static async Task TabUntilFocusedAsync(IPage page, ILocator locator, int maxTabs = 8)
    {
        for (var i = 0; i < maxTabs; i++)
        {
            await page.Keyboard.PressAsync("Tab");
            var isFocused = await locator.EvaluateAsync<bool>(
                "element => element === document.activeElement");
            if (isFocused)
            {
                return;
            }
        }

        await Expect(locator).ToBeFocusedAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
