// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class CharactersPage(IPage page)
{
    private readonly IPage _page = page;

    // The page heading — "My Characters"
    public ILocator Heading =>
        _page.GetByText("My Characters");

    // Character card buttons rendered in the characters grid.
    public ILocator CharacterList =>
        _page.Locator(".characters-grid .character-card");

    public ILocator CharacterCard(string characterName) =>
        CharacterList.Filter(new()
        {
            Has = _page.GetByText(characterName, new() { Exact = true })
        });

    // FluentTextField — target inner input for FillAsync to work.
    public ILocator DeleteConfirmationField =>
        _page.Locator("fluent-text-field[placeholder='FORGET ME'] input");

    // The delete account button
    public ILocator DeleteAccountButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Delete My Account" });

    public ILocator AccountDefaultState =>
        _page.Locator(".character-account-state");

    public ILocator DeleteAccountDangerZone =>
        _page.Locator(".characters-danger-zone");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/characters",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }

    public async Task<int> GetCharacterCountAsync()
    {
        return await CharacterList.CountAsync();
    }
}
