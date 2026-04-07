using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class CreateRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Authenticated_raider_can_see_create_run_form()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");

        // Blazor CreateRunPage renders "Create Run" as H3
        await Expect(_page.GetByText("Create Run").First).ToBeVisibleAsync();

        // Wait for loading to finish (instances load)
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Form fields are present: FluentSelect for Instance and Visibility,
        // FluentTextField for Mode Key, Start Time, Signup Close Time, Description
        await Expect(_page.Locator("#instance-select")).ToBeVisibleAsync();
        await Expect(_page.Locator("#modekey-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#starttime-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#signupclose-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#visibility-select")).ToBeVisibleAsync();
        await Expect(_page.Locator("#description-input")).ToBeVisibleAsync();

        // Create Run and Cancel buttons
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Create Run" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Authenticated_raider_can_fill_and_submit_create_run_form()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");

        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Select an instance — Deadmines NORMAL:5 (value "63:NORMAL:5")
        var instanceSelect = _page.Locator("#instance-select");
        await instanceSelect.EvaluateAsync(
            "(el, val) => { el.value = val; el.dispatchEvent(new Event('change', {bubbles: true})); }",
            "63:NORMAL:5");

        // Fill ISO 8601 text fields
        await _page.Locator("#modekey-input").FillAsync("NORMAL:5");
        await _page.Locator("#starttime-input").FillAsync("2026-06-01T20:00:00Z");
        await _page.Locator("#signupclose-input").FillAsync("2026-06-01T18:00:00Z");
        await _page.Locator("#description-input").FillAsync("E2E test run");

        // Submit the form — button should be enabled now
        var createButton = _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });
        await createButton.ClickAsync();

        // After successful creation, should redirect to the new run's detail page
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/[^/]+$"), new() { Timeout = 10_000 });
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
