// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class LoginFailedPage(IPage page)
{
    private readonly IPage _page = page;

    public ILocator ErrorHeading =>
        _page.GetByText("Login Failed", new() { Exact = true });

    public ILocator ErrorMessage =>
        _page.GetByText("Something went wrong while signing in. Please try again.");

    public ILocator TryAgainButton =>
        _page.GetByRole(AriaRole.Link, new() { Name = "Try Again" });

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/auth/failure", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsErrorVisibleAsync()
    {
        return await ErrorHeading.IsVisibleAsync();
    }
}
