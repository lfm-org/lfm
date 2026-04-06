using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class LandingPageSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Root_route_renders_a_restrained_public_landing_page()
    {
        await _page.GotoAsync(fixture.AppBaseUrl);

        var main = _page.GetByRole(AriaRole.Main);

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"\/$"));
        await Expect(main.GetByText("\ud83c\udf00 LFM", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(main.GetByRole(AriaRole.Heading, new() { Name = "Plan runs in one place" })).ToBeVisibleAsync();
        await Expect(main.GetByText("Create runs, collect signups, and check roster coverage before invite time.")).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Link, new() { Name = "Login" })).ToBeVisibleAsync();
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = "Sign In To Plan Runs" })).ToHaveCountAsync(0);
        await Expect(main.GetByRole(AriaRole.Link, new() { Name = "Battle.net Login" })).ToHaveCountAsync(0);
        await Expect(main.GetByText("Shared schedule")).ToBeVisibleAsync();
        await Expect(main.GetByText("Keep upcoming runs and signups in one place.")).ToBeVisibleAsync();
        await Expect(main.GetByText("Role coverage")).ToBeVisibleAsync();
        await Expect(main.GetByText("See tank, healer, and DPS coverage at a glance.")).ToBeVisibleAsync();
        await Expect(main.GetByText("Battle.net sign-in")).ToBeVisibleAsync();
        await Expect(main.GetByText("Players sign in with Battle.net and use their saved characters.")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
