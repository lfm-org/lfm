// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class GuildPage(IPage page)
{
    private readonly IPage _page = page;

    // Page heading rendered by FluentLabel Typo=H3. Use typo attribute to disambiguate from nav links.
    public ILocator Heading =>
        _page.Locator("[typo='h3']").Filter(new() { HasTextString = "Guild" });

    // The guild name heading rendered inside the card (H4)
    public ILocator GuildNameHeading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Test Guild" });

    // The status card — always visible when guild data loads
    public ILocator StatusCard =>
        _page.GetByText("Status");

    // "No guild found" card when the raider has no guild
    public ILocator NoGuildCard =>
        _page.GetByText("No guild found");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/guild",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
