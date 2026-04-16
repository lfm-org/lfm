// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class PrivacyPage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The "Privacy Policy" heading rendered by FluentLabel H2.
    /// FluentLabel does not emit a semantic heading element; match by visible text.
    /// </summary>
    public ILocator Heading =>
        _page.GetByText("Privacy Policy");

    /// <summary>Any section of the privacy policy content (Data Controller section).</summary>
    public ILocator DataControllerSection =>
        _page.GetByText("Data Controller");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/privacy", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
