using Lfm.E2E.Fixtures;
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
        _context = await fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Characters_page_shows_empty_state_when_the_raider_has_no_account_characters()
    {
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fcharacters");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Select your character" })).ToBeVisibleAsync();
        await Expect(_page.GetByText("No Battle.net characters found.")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
