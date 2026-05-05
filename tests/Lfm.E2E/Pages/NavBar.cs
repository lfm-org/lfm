// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class NavBar(IPage page)
{
    private readonly IPage _page = page;

    public ILocator AccountMenuButton =>
        _page.Locator(".account-menu-trigger");

    public ILocator SignOutButton =>
        _page.Locator(".account-menu").GetByRole(AriaRole.Button, new() { Name = "Sign Out" });

    public ILocator SignInLink =>
        _page.Locator("fluent-anchor[href='/login']");

    public ILocator RunsLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Runs", Exact = true });

    public ILocator GuildLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Guild", Exact = true });

    public ILocator CharactersLink =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Characters", Exact = true });

    public async Task<bool> IsSignOutVisibleAsync()
    {
        return await SignOutButton.IsVisibleAsync();
    }

    public async Task ClickSignOutAsync()
    {
        await AccountMenuButton.ClickAsync();
        await SignOutButton.ClickAsync();
    }
}
