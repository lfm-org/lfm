// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class NotFoundPage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The "Not Found" h3 heading rendered by the NotFound.razor component.
    /// This is a plain semantic &lt;h3&gt; element.
    /// </summary>
    public ILocator Heading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Not Found" });

    /// <summary>The descriptive message below the heading.</summary>
    public ILocator Message =>
        _page.GetByText("Sorry, the content you are looking for does not exist.");

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
