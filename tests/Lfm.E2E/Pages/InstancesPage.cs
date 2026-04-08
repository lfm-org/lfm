using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class InstancesPage(IPage page)
{
    private readonly IPage _page = page;

    /// <summary>
    /// The "Instances" heading rendered by FluentLabel H3.
    /// FluentLabel does not emit a semantic heading element; match by visible text.
    /// </summary>
    public ILocator Heading =>
        _page.GetByText("Instances");

    /// <summary>The data grid that lists raid instances.</summary>
    public ILocator InstanceGrid =>
        _page.Locator("fluent-data-grid");

    /// <summary>Individual rows within the instance list grid.</summary>
    public ILocator InstanceRows =>
        _page.Locator("fluent-data-grid-row[row-type='default']");

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/instances", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
