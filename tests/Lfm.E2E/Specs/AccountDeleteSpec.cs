using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class AccountDeleteSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Authenticated_raiders_can_permanently_delete_their_account_from_the_characters_page()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fcharacters&testAuthScenario=delete-account");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/characters$"));

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Select your character" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Farewell", RegexOptions.IgnoreCase) })).ToBeVisibleAsync();

        var deleteButton = _page.GetByRole(AriaRole.Button, new() { Name = "Forget me" });
        await Expect(deleteButton).ToBeDisabledAsync();

        await _page.GetByLabel("Type FORGET ME to confirm").FillAsync("forget me");
        await Expect(deleteButton).ToBeDisabledAsync();

        await _page.GetByLabel("Type FORGET ME to confirm").FillAsync("FORGET ME");
        await Expect(deleteButton).ToBeEnabledAsync();
        await deleteButton.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/goodbye$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Account deleted" })).ToBeVisibleAsync();

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\?redirect=%2Fruns$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Sign in with Battle.net" })).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
