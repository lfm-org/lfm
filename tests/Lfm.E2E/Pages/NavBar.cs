using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class NavBar(IPage page)
{
    private readonly IPage _page = page;

    public ILocator SignOutButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Sign Out" });

    public ILocator SignInLink =>
        _page.Locator("fluent-anchor[href='/login']");

    public ILocator RunsLink =>
        _page.Locator("fluent-anchor[href='/runs']");

    public ILocator GuildLink =>
        _page.Locator("fluent-anchor[href='/guild']");

    public ILocator CharactersLink =>
        _page.Locator("fluent-anchor[href='/characters']");

    public async Task<bool> IsSignOutVisibleAsync()
    {
        return await SignOutButton.IsVisibleAsync();
    }

    public async Task ClickSignOutAsync()
    {
        await SignOutButton.ClickAsync();
    }
}
