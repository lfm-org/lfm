using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LoginPage(IPage page)
{
    private readonly IPage _page = page;

    public ILocator Heading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Sign In" });

    public ILocator SignInButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/login");
    }

    public async Task<bool> IsSignInButtonVisibleAsync()
    {
        return await SignInButton.IsVisibleAsync();
    }

    public async Task ClickSignInAsync()
    {
        await SignInButton.ClickAsync();
    }
}
