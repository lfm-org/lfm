using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
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
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl,
            redirect: "/characters",
            battleNetId: "test-bnet-id-delete-account");
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Authenticated_raiders_can_permanently_delete_their_account_from_the_characters_page()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/characters");

        // Blazor CharactersPage renders "My Characters" as H3 and "Delete Account" as H4
        await Expect(_page.GetByText("My Characters")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Delete Account")).ToBeVisibleAsync();

        // The delete button text is "Delete My Account"
        var deleteButton = _page.GetByRole(AriaRole.Button, new() { Name = "Delete My Account" });
        await Expect(deleteButton).ToBeDisabledAsync();

        // Fill in the confirmation text — the FluentTextField has Placeholder="FORGET ME"
        var confirmInput = _page.Locator("fluent-text-field input");
        await confirmInput.FillAsync("wrong text");
        await Expect(deleteButton).ToBeDisabledAsync();

        await confirmInput.FillAsync("FORGET ME");
        await Expect(deleteButton).ToBeEnabledAsync();
        await deleteButton.ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/goodbye$"));
        // Blazor GoodbyePage renders "Goodbye" as H2
        await Expect(_page.GetByText("Goodbye")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Your account has been deleted")).ToBeVisibleAsync();

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/login\?redirect=%2Fruns$"));
        await Expect(_page.GetByText("Sign In")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
