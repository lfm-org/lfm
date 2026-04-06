using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task Confirmation_dialog_cancel_preserves_the_run()
    {
        // run-guild-sparse-icc10 is created by the default test user
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild ten-player alt run" });
        await Expect(runCard).ToBeVisibleAsync();

        await runCard.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(_page.GetByText("Delete run?")).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await Expect(_page.GetByText("Delete run?")).ToHaveCountAsync(0);
        await Expect(runCard).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Non_creator_without_delete_permission_cannot_see_delete_button_on_guild_runs()
    {
        // Default test user (rank 2) does not have canDeleteGuildRuns
        // run-guild-dense-molten-core is created by guild-raider-03
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild retro forty-player night" });
        await Expect(runCard).ToBeVisibleAsync();
        await Expect(runCard.GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task No_delete_button_on_public_runs_created_by_someone_else()
    {
        // run-public-signup-target-icc25 is created by guild-raider-01
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Heroic farm night" });
        await Expect(runCard).ToBeVisibleAsync();
        await Expect(runCard.GetByRole(AriaRole.Button, new() { Name = "Delete" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Creator_can_delete_own_guild_run_even_without_canDeleteGuildRuns_permission()
    {
        // Default test user (rank 2, canDeleteGuildRuns=false) created run-guild-sparse-icc10
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild ten-player alt run" });
        await Expect(runCard).ToBeVisibleAsync();

        await runCard.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(_page.GetByText("Delete run?")).ToBeVisibleAsync();
        await Expect(_page.GetByText(new Regex("Guild ten-player alt run"))).ToBeVisibleAsync();

        var deleteTask = _page.WaitForRequestAsync(req =>
            req.Method == "DELETE" && req.Url.Contains("/api/runs/"));
        await _page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).Last.ClickAsync();
        await deleteTask;

        await Expect(runCard).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Guild_master_can_delete_guild_run_created_by_another_member()
    {
        // Re-authenticate as guild master (rank 0) who has canDeleteGuildRuns
        await _page.GotoAsync(fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns&testAuthScenario=guild-master");
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));

        // run-guild-dense-molten-core is created by guild-raider-03
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild retro forty-player night" });
        await Expect(runCard).ToBeVisibleAsync();

        await runCard.GetByRole(AriaRole.Button, new() { Name = "Delete" }).ClickAsync();
        await Expect(_page.GetByText("Delete run?")).ToBeVisibleAsync();

        var deleteTask = _page.WaitForRequestAsync(req =>
            req.Method == "DELETE" && req.Url.Contains("/api/runs/"));
        await _page.GetByRole(AriaRole.Button, new() { Name = "Delete" }).Last.ClickAsync();
        await deleteTask;

        await Expect(runCard).ToHaveCountAsync(0);
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
