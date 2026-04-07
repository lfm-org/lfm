using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class RunsPage(IPage page)
{
    private readonly IPage _page = page;

    // "Create Run" button is unique to the runs page and confirms the page
    // has fully loaded (the nav bar also has a "Runs" link, making text matching ambiguous).
    public ILocator Heading =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
