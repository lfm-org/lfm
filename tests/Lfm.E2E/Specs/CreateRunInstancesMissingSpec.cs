using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("instances-missing")]
public class CreateRunInstancesMissingSpec(InstancesMissingFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Create_run_shows_error_when_instances_are_unavailable()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();
        await Expect(_page.GetByText("Failed to load instances")).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
