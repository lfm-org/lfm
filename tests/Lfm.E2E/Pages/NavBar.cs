using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class NavBar(IPage page)
{
    private readonly IPage _page = page;

    // FluentUI's <fluent-button> custom element exposes role="button" via a JS
    // interop upgrade that races the initial Blazor WASM bootstrap on cold CI
    // runs. `GetByRole(Button)` misses it intermittently; matching by tag and
    // visible text is upgrade-independent. See issue #45.
    public ILocator SignOutButton =>
        _page.Locator("fluent-button:has-text('Sign Out')");

    public ILocator SignInLink =>
        _page.Locator("fluent-anchor[href='/login']");

    public ILocator RunsLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Runs", Exact = true });

    public ILocator GuildLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Guild", Exact = true });

    public ILocator CharactersLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Characters", Exact = true });

    public async Task<bool> IsSignOutVisibleAsync()
    {
        return await SignOutButton.IsVisibleAsync();
    }

    public async Task ClickSignOutAsync()
    {
        await SignOutButton.ClickAsync();
    }
}
