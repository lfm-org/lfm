// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace Lfm.E2E.Pages;

public class GuildAdminPage(IPage page)
{
    private readonly IPage _page = page;

    // The page heading — "Guild Admin"
    public ILocator Heading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Guild Admin" });

    // The "Override Settings" section heading
    public ILocator OverrideSettingsHeading =>
        _page.GetByText("Override Settings");

    public ILocator RankPermissionsHeading =>
        _page.GetByText("Rank Permissions");

    public ILocator RankLabel(int rank) =>
        _page.GetByText($"Rank {rank}", new() { Exact = true });

    public ILocator GuildIdInput =>
        _page.Locator("#guild-admin-guild-id");

    public ILocator LoadButton =>
        _page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Load guild|Lataa kilta") });

    public ILocator GuildName(string name) =>
        _page.GetByText(name, new() { Exact = true });

    public ILocator GuildIdChip(string guildId) =>
        _page.GetByText($"Guild ID {guildId}", new() { Exact = true });

    // FluentTextArea — target inner textarea for FillAsync to work.
    public ILocator SloganField =>
        _page.Locator("#guild-slogan textarea");

    // The "Save Settings" submit button
    public ILocator SaveButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Save Settings" });

    // Success message banner shown after a save
    public ILocator SuccessMessage =>
        _page.GetByText("Settings saved successfully.");

    public ILocator UnsavedDialog =>
        _page.GetByRole(AriaRole.Alertdialog)
            .Filter(new() { HasTextString = "Unsaved changes" });

    public ILocator StayButton =>
        UnsavedDialog.GetByRole(AriaRole.Button, new() { Name = "Stay" });

    public ILocator LeaveButton =>
        UnsavedDialog.GetByRole(AriaRole.Button, new() { Name = "Leave" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/guild/admin",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task LoadGuildAsync(string guildId)
    {
        await GuildIdInput.EvaluateAsync(
            """
            (element, value) => {
                element.value = value;
                element.currentValue = value;
                element.setAttribute('current-value', value);
                element.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                element.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
            }
            """,
            guildId);
        await LoadButton.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await LoadButton.ClickAsync();
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
