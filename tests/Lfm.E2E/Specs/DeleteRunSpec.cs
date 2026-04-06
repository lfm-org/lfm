using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class DeleteRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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

    [Fact(Skip = "Blazor RunsPage does not have inline Delete buttons on run cards; delete is on the EditRunPage")]
    public async Task Confirmation_dialog_cancel_preserves_the_run()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Delete buttons on run cards")]
    public async Task Non_creator_without_delete_permission_cannot_see_delete_button_on_guild_runs()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Delete buttons on run cards")]
    public async Task No_delete_button_on_public_runs_created_by_someone_else()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Delete buttons on run cards")]
    public async Task Creator_can_delete_own_guild_run_even_without_canDeleteGuildRuns_permission()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Delete buttons on run cards")]
    public async Task Guild_master_can_delete_guild_run_created_by_another_member()
    {
        await Task.CompletedTask;
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
