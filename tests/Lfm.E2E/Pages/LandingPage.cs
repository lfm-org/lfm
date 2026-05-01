// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LandingPage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The main heading "Looking For More" rendered by FluentLabel H1.
    /// FluentLabel does not emit a semantic &lt;h1&gt; element, so we match by visible text.
    /// </summary>
    public ILocator Heading =>
        _page.GetByText("Looking For More");

    /// <summary>Primary sign-in call-to-action button.</summary>
    public ILocator SignInButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

}
