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

    // Character cards rendered in the grid (FluentCard elements within the grid div)
    public ILocator CharacterList =>
        _page.Locator("div[style*='grid-template-columns'] fluent-card");

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
