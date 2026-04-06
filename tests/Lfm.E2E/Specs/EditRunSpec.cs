using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class EditRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Creator_sees_edit_button_on_own_run_with_no_signups()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-empty-deadmines");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Public dungeon warmup" });
        await Expect(runCard).ToBeVisibleAsync();
        await Expect(runCard.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToBeEnabledAsync();
    }

    [Fact]
    public async Task No_edit_button_on_runs_created_by_someone_else()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-signup-target-icc25");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Heroic farm night" });
        await Expect(runCard).ToBeVisibleAsync();
        await Expect(runCard.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Edit_button_disabled_when_signup_close_time_passed()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-edit-closed-deadmines");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Edit closed test run" });
        await Expect(runCard).ToBeVisibleAsync();
        await Expect(runCard.GetByRole(AriaRole.Button, new() { Name = "Edit" })).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Creator_sees_locked_instance_and_start_time_when_run_has_signups()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-guild-sparse-icc10");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Guild ten-player alt run" });
        await Expect(runCard).ToBeVisibleAsync();

        await runCard.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/run-guild-sparse-icc10\/edit$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Edit Run" })).ToBeVisibleAsync();

        // Instance and mode selects should be disabled (locked after signups)
        var instanceCombobox = _page.GetByRole(AriaRole.Combobox).First;
        await Expect(instanceCombobox).ToHaveAttributeAsync("aria-disabled", "true");

        // Description should be editable
        await Expect(_page.GetByLabel("Description")).ToBeEnabledAsync();
    }

    [Fact]
    public async Task Creator_can_edit_own_run_with_no_signups_and_save_changes()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-empty-deadmines");
        var runCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Public dungeon warmup" });
        await runCard.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\/run-public-empty-deadmines\/edit$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Edit Run" })).ToBeVisibleAsync();

        await _page.GetByLabel("Description").FillAsync("Edited dungeon warmup");

        var requestTask = _page.WaitForRequestAsync(req =>
            req.Method == "PUT" && req.Url.Contains("/api/runs/run-public-empty-deadmines"));
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" }).ClickAsync();
        await requestTask;

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs\?run=run-public-empty-deadmines"));
        var updatedCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Edited dungeon warmup" });
        await Expect(updatedCard).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
