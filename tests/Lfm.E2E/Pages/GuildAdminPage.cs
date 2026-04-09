using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class GuildAdminPage(IPage page)
{
    private readonly IPage _page = page;

    // The page heading — "Guild Admin"
    public ILocator Heading =>
        _page.GetByText("Guild Admin");

    // The "Override Settings" section heading
    public ILocator OverrideSettingsHeading =>
        _page.GetByText("Override Settings");

    // FluentTextArea web component — target outer element so Blazor @bind-Value fires.
    public ILocator SloganField =>
        _page.Locator("#guild-slogan").First;

    // The "Save Settings" submit button
    public ILocator SaveButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" });

    // Success message banner shown after a save
    public ILocator SuccessMessage =>
        _page.GetByText("Settings saved successfully.");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/guild/admin",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
