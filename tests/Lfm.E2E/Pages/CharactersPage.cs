using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class CharactersPage(IPage page)
{
    private readonly IPage _page = page;

    // The page heading — "My Characters"
    public ILocator Heading =>
        _page.GetByText("My Characters");

    // The refresh button — "Refresh from Battle.net"
    public ILocator RefreshButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Refresh from Battle.net" });

    // Character cards rendered in the grid (FluentCard elements within the grid div)
    public ILocator CharacterList =>
        _page.Locator("div[style*='grid-template-columns'] fluent-card");

    // The delete account confirmation text field.
    // FluentTextField renders both a wrapper and inner input with the same placeholder,
    // so target the outer fluent-text-field element directly.
    public ILocator DeleteConfirmationField =>
        _page.Locator("fluent-text-field[placeholder='FORGET ME']");

    // The delete account button
    public ILocator DeleteAccountButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Delete My Account" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }

    public async Task ClickRefreshAsync()
    {
        await RefreshButton.ClickAsync();
    }

    public async Task<int> GetCharacterCountAsync()
    {
        return await CharacterList.CountAsync();
    }
}
