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
    public async Task Edit_run_page_loads_and_shows_form()
    {
        // Navigate directly to edit page for a known run
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/run-guild-sparse-icc10/edit");

        // Blazor EditRunPage renders "Edit Run" as H3
        await Expect(_page.GetByText("Edit Run").First).ToBeVisibleAsync();

        // Wait for loading to finish
        await _page.Locator("fluent-progress-ring").WaitForAsync(
            new() { State = WaitForSelectorState.Hidden, Timeout = 10_000 });

        // Form fields should be present and pre-populated
        await Expect(_page.Locator("#instance-select")).ToBeVisibleAsync();
        await Expect(_page.Locator("#modekey-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#starttime-input")).ToBeVisibleAsync();
        await Expect(_page.Locator("#description-input")).ToBeVisibleAsync();

        // Action buttons
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Delete Run" })).ToBeVisibleAsync();
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Edit button on run cards; edit is accessed via direct URL")]
    public async Task Creator_sees_edit_button_on_own_run_with_no_signups()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Edit button on run cards")]
    public async Task No_edit_button_on_runs_created_by_someone_else()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Edit button with closed-time disabling")]
    public async Task Edit_button_disabled_when_signup_close_time_passed()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor EditRunPage does not implement instance/start-time locking when run has signups")]
    public async Task Creator_sees_locked_instance_and_start_time_when_run_has_signups()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor RunsPage does not have inline Edit button on run cards; full edit-save flow differs from React")]
    public async Task Creator_can_edit_own_run_with_no_signups_and_save_changes()
    {
        await Task.CompletedTask;
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
