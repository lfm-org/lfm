// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LoginPage(IPage page)
{
    private readonly IPage _page = page;

    // FluentLabel with Typography does not render as a semantic heading element,
    // so we match on the unique descriptive text that appears only on the login page.
    public ILocator Heading =>
        _page.GetByText("Connect your Battle.net account to get started.");

    public ILocator SignInButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });

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
