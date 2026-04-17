// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class GoodbyePage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The "Goodbye" heading rendered by FluentLabel H2.
    /// FluentLabel does not emit a semantic heading element; match by visible text.
    /// </summary>
    public ILocator Heading =>
        _page.GetByText("Goodbye");

    /// <summary>Confirmation message shown after account deletion.</summary>
    public ILocator AccountDeletedMessage =>
        _page.GetByText("Your account has been deleted.");

    /// <summary>The "Sign In Again" button linking back to /login.</summary>
    public ILocator SignInAgainButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Sign In Again" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/goodbye", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
