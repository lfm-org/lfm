using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LoginFailedPage(IPage page)
{
    private readonly IPage _page = page;

    public ILocator ErrorHeading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Login Failed" });

    public ILocator ErrorMessage =>
        _page.GetByText("Something went wrong while signing in. Please try again.");

    public ILocator TryAgainButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Try Again" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/auth/failure");
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ErrorHeading.IsVisibleAsync();
    }
}
