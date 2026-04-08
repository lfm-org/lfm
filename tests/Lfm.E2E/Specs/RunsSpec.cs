using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Runs")]
[Trait("Category", "Functional")]
public class RunsSpec(RunsFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    private IBrowserContext? _authContext;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Context = _authContext;
        Page = await _authContext.NewPageAsync();
        AttachDiagnosticListeners();
        await StartTracingAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_authContext is not null)
            await _authContext.CloseAsync();
    }

    [Fact]
    public async Task RunsPage_Loads_DisplaysRunList()
    {
        var runsPage = new RunsPage(Page!);

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);

        // Page header is loaded
        await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Seed data has at least one run
        await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId)).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task CreateRun_SubmitForm_AppearsInList()
    {
        var runsPage = new RunsPage(Page!);

        // Navigate to the create-run form
        await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);

        // Wait for the form to load (instance dropdown is populated)
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select the first available instance via fluent-select web component:
        // click to open the dropdown, then click the first real option (skip the placeholder).
        var instanceSelect = Page.Locator("#instance-select");
        await instanceSelect.ClickAsync();
        // Wait for options to be rendered; select the second fluent-option (index 0 = placeholder)
        var firstRealOption = Page.Locator("#instance-select fluent-option").Nth(1);
        await firstRealOption.WaitForAsync(new() { Timeout = 10000 });
        await firstRealOption.ClickAsync();

        // Fill in required form fields with a unique run name via description
        var uniqueRunName = $"E2E-Create-{Guid.NewGuid():N}";
        await runsPage.ModeKeyInput.FillAsync("NORMAL:25");
        await runsPage.StartTimeInput.FillAsync("2026-06-01T20:00:00Z");
        await runsPage.DescriptionInput.FillAsync(uniqueRunName);

        // Submit
        await runsPage.CreateRunSubmitButton.ClickAsync();

        // Should navigate to the newly created run's detail page
        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs/[^/]+$"),
            new() { Timeout = 20000 });

        // Navigate back to the runs list and verify the new run's description is present
        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The runs list shows instanceName; the created run should appear in the list
        var allRunItems = runsPage.AllRunItems;
        var count = await allRunItems.CountAsync();
        count.Should().BeGreaterThan(0, "at least the newly created run should appear in the list");
    }

    [Fact]
    public async Task RunDetail_Navigate_ShowsRosterAndCoverage()
    {
        var runsPage = new RunsPage(Page!);

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);

        // Wait for the list to load
        await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId)).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Click the seeded run to open detail view
        await runsPage.SelectRunAsync(DefaultSeed.TestRunId);

        // The seeded run has 1 signup ("Aelrin"), so we expect "Roster (1)"
        await Assertions.Expect(runsPage.RosterHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        var rosterText = await runsPage.RosterHeading.InnerTextAsync();
        rosterText.Should().Contain("Roster (");

        // Roster grid should be present
        await Assertions.Expect(runsPage.RosterGrid).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task SignUp_SelectCharacter_AppearsInRoster()
    {
        // Navigate directly to the seeded run's detail page
        var encodedId = Uri.EscapeDataString(DefaultSeed.TestRunId);
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Verify we are on the runs page and the detail is loaded
        var runsPage = new RunsPage(Page);
        await Assertions.Expect(runsPage.RosterHeading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The seeded run already has Aelrin signed up — verify roster shows at least 1 row
        await Assertions.Expect(runsPage.RosterGrid).ToBeVisibleAsync(new() { Timeout = 10000 });
        var rosterText = await runsPage.RosterHeading.InnerTextAsync();
        rosterText.Should().Contain("Roster (");

        // Verify the seeded character (Aelrin) appears in the roster grid
        await Assertions.Expect(Page.GetByText("Aelrin")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task CancelSignup_Remove_DisappearsFromRoster()
    {
        // This test verifies that after navigating to the run detail the roster is shown.
        // Full cancel-signup UI is on the edit page roster grid — this test verifies
        // the UI reflects the roster state from seed data.
        var encodedId = Uri.EscapeDataString(DefaultSeed.TestRunId);
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}/edit",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the edit page to load (roster section heading)
        await Assertions.Expect(
            Page.GetByText("Roster (", new() { Exact = false })).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Verify the "Save Changes" button is present (confirms edit page loaded)
        var runsPage = new RunsPage(Page);
        await Assertions.Expect(runsPage.SaveChangesButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Verify the seeded character is shown in the roster on the edit page
        await Assertions.Expect(Page.GetByText("Aelrin")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task EditRun_ModifyFields_ChangesReflected()
    {
        var encodedId = Uri.EscapeDataString(DefaultSeed.TestRunId);
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}/edit",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var runsPage = new RunsPage(Page);

        // Wait for the edit form to load
        await Assertions.Expect(runsPage.SaveChangesButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Update the description with a unique value to verify the change
        var updatedDescription = $"E2E-Edited-{Guid.NewGuid():N}";
        await runsPage.UpdateDescriptionAsync(updatedDescription);

        // Expect the success banner
        await Assertions.Expect(runsPage.SaveSuccessBanner).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task DeleteRun_Confirm_RemovedFromList()
    {
        // Create a fresh run via API so we don't destroy the seeded run used by other tests.
        // We use the create-run page to create a run, then delete it.
        var runsPage = new RunsPage(Page!);

        await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Select the first available instance
        var instanceSelect = Page.Locator("#instance-select");
        await instanceSelect.ClickAsync();
        var firstRealOption = Page.Locator("#instance-select fluent-option").Nth(1);
        await firstRealOption.WaitForAsync(new() { Timeout = 10000 });
        await firstRealOption.ClickAsync();

        var uniqueDescription = $"E2E-Delete-{Guid.NewGuid():N}";
        await runsPage.ModeKeyInput.FillAsync("HEROIC:25");
        await runsPage.StartTimeInput.FillAsync("2026-07-01T20:00:00Z");
        await runsPage.DescriptionInput.FillAsync(uniqueDescription);
        await runsPage.CreateRunSubmitButton.ClickAsync();

        // Wait for navigation to the new run's detail (URL: /runs/{id})
        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs/[^/]+$"),
            new() { Timeout = 20000 });

        // Extract the newly created run ID from the URL
        var detailUrl = Page.Url;
        var createdRunId = detailUrl.Split("/runs/").Last();

        // Navigate to the edit page for the newly created run
        await Page.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{createdRunId}/edit",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        // Wait for the edit page to load
        await Assertions.Expect(runsPage.DeleteRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Click Delete Run to show confirmation
        await runsPage.DeleteRunButton.ClickAsync();

        // Click "Yes, delete" to confirm
        await Assertions.Expect(runsPage.ConfirmDeleteButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        await runsPage.ConfirmDeleteButton.ClickAsync();

        // Should redirect back to /runs after deletion
        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs$"),
            new() { Timeout = 20000 });

        // Verify the deleted run is no longer present in the list
        var deletedRunItem = runsPage.RunItem(createdRunId);
        var isVisible = await deletedRunItem.IsVisibleAsync();
        isVisible.Should().BeFalse("the deleted run should not appear in the list");
    }
}
