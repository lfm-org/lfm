using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LoginPage(IPage page)
{
    private readonly IPage _page = page;

    // FluentLabel with Typography does not render as a semantic heading element,
    // so we match on the unique descriptive text that appears only on the login page.
    public ILocator Heading =>
        _page.GetByText("Connect your Battle.net account to get started.");

    // FluentUI's <fluent-button> custom element exposes role="button" via a JS
    // interop upgrade that races the initial Blazor WASM bootstrap on cold CI
    // runs. `GetByRole(Button)` misses it intermittently; matching by tag and
    // visible text is upgrade-independent because the text sits in the light
    // DOM from the moment the component renders. See issue #45 and the same
    // workaround in RunsSpec.DeleteRun (microsoft/fluentui-blazor#2614).
    public ILocator SignInButton =>
        _page.Locator("fluent-button:has-text('Sign in with Battle.net')");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/login", new() { WaitUntil = WaitUntilState.NetworkIdle });
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
