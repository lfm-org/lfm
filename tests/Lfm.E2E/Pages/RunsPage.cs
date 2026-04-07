using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class RunsPage(IPage page)
{
    private readonly IPage _page = page;

    public ILocator Heading =>
        _page.GetByRole(AriaRole.Heading, new() { Name = "Runs" });

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }
}
