using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class GuildPage(IPage page)
{
    private readonly IPage _page = page;

    // The page heading — "Guild"
    public ILocator Heading =>
        _page.GetByText("Guild");

    // The guild name heading rendered inside the card
    public ILocator GuildNameHeading =>
        _page.GetByText("Test Guild");

    // The status card — always visible when guild data loads
    public ILocator StatusCard =>
        _page.GetByText("Status");

    // "No guild found" card when the raider has no guild
    public ILocator NoGuildCard =>
        _page.GetByText("No guild found");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/guild",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
