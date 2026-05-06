// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class InstancesPage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The top-level "Instances" heading.
    /// </summary>
    public ILocator Heading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Instances", Exact = true });

    /// <summary>
    /// The data grid that lists raid instances. FluentDataGrid compiles to a
    /// <c>&lt;table class="fluent-data-grid"&gt;</c> element — not a custom
    /// <c>&lt;fluent-data-grid&gt;</c> HTML element.
    /// </summary>
    public ILocator InstanceGrid =>
        _page.Locator("table.fluent-data-grid");

    public ILocator Summary =>
        _page.Locator(".instances-summary");

    public ILocator TableSurface =>
        _page.Locator(".instances-table-surface");

    /// <summary>
    /// Individual data rows within the instance grid. Header rows have
    /// <c>row-type="header"</c>; data rows are plain <c>&lt;tr class="fluent-data-grid-row"&gt;</c>
    /// without a <c>row-type</c> attribute.
    /// </summary>
    public ILocator InstanceRows =>
        _page.Locator("tr.fluent-data-grid-row:not([row-type])");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/instances", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
