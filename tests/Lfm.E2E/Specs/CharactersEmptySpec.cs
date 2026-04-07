using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("characters-empty")]
public class CharactersEmptySpec(CharactersEmptyFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl,
            redirect: "/characters");
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Characters_page_shows_empty_state_when_the_raider_has_no_account_characters()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/characters");

        // Blazor CharactersPage renders "My Characters" as H3
        await Expect(_page.GetByText("My Characters")).ToBeVisibleAsync();
        // Empty state message from the Blazor page
        await Expect(_page.GetByText("No characters found")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
