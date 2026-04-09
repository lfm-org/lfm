using Microsoft.Playwright;

namespace Lfm.E2E.Pages;

public class RunsPage(IPage page)
{
    private readonly IPage _page = page;

    // ---- Runs list page ----

    /// <summary>
    /// "Create Run" button — unique to the runs page header and confirms the page
    /// has fully loaded (the nav bar also has a "Runs" link, making text matching ambiguous).
    /// </summary>
    public ILocator Heading =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });

    /// <summary>"Create Run" button in the page header (navigates to /runs/new).</summary>
    public ILocator CreateRunButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });

    /// <summary>"Refresh" button in the page header.</summary>
    public ILocator RefreshButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Refresh" });

    /// <summary>A run item in the list panel, identified by its run ID data-testid attribute.</summary>
    public ILocator RunItem(string runId) =>
        _page.Locator($"[data-testid='run-item-{runId}']");

    /// <summary>All run items in the list panel.</summary>
    public ILocator AllRunItems =>
        _page.Locator("[data-testid^='run-item-']");

    // ---- Run detail panel (visible after selecting a run) ----

    /// <summary>The "Roster (N)" heading in the run detail panel.</summary>
    public ILocator RosterHeading =>
        _page.GetByText("Roster (", new() { Exact = false });

    /// <summary>Roster data grid on the run detail panel.</summary>
    public ILocator RosterGrid =>
        _page.Locator("fluent-data-grid");

    /// <summary>"Edit" button in the run detail panel (links to /runs/{id}/edit).</summary>
    public ILocator EditButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Edit" });

    /// <summary>"Select a run to see details." placeholder text.</summary>
    public ILocator SelectRunPlaceholder =>
        _page.GetByText("Select a run to see details.");

    // ---- Create Run form (/runs/new) ----

    /// <summary>Instance dropdown on the create-run form.</summary>
    public ILocator InstanceSelect =>
        _page.Locator("#instance-select").First;

    /// <summary>Mode Key text field — inner input of FluentTextField.</summary>
    public ILocator ModeKeyInput =>
        _page.Locator("#modekey-input input");

    /// <summary>Start Time text field — inner input of FluentTextField.</summary>
    public ILocator StartTimeInput =>
        _page.Locator("#starttime-input input");

    /// <summary>Signup Close Time text field — inner input of FluentTextField.</summary>
    public ILocator SignupCloseTimeInput =>
        _page.Locator("#signupclose-input input");

    /// <summary>Visibility dropdown on the create-run form.</summary>
    public ILocator VisibilitySelect =>
        _page.Locator("#visibility-select").First;

    /// <summary>Description text field — inner input of FluentTextField.</summary>
    public ILocator DescriptionInput =>
        _page.Locator("#description-input input");

    /// <summary>"Create Run" submit button on the create-run form.</summary>
    public ILocator CreateRunSubmitButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });

    /// <summary>"Cancel" button on the create-run form.</summary>
    public ILocator CreateRunCancelButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

    // ---- Edit Run form (/runs/{id}/edit) ----

    /// <summary>"Save Changes" button on the edit-run form.</summary>
    public ILocator SaveChangesButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Save Changes" });

    /// <summary>"Delete Run" button on the edit-run form (opens delete confirmation).</summary>
    public ILocator DeleteRunButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Delete Run" });

    /// <summary>"Yes, delete" button in the delete confirmation card.</summary>
    public ILocator ConfirmDeleteButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" });

    /// <summary>"Cancel" button in the delete confirmation card.</summary>
    public ILocator DeleteCancelButton =>
        _page.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

    /// <summary>"Run saved successfully." success banner.</summary>
    public ILocator SaveSuccessBanner =>
        _page.GetByText("Run saved successfully.");

    // ---- Actions ----

    public async Task<bool> IsLoadedAsync()
    {
        return await Heading.IsVisibleAsync();
    }

    public async Task GotoAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/runs", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    public async Task NavigateToCreateRunAsync(string appBaseUrl)
    {
        await _page.GotoAsync($"{appBaseUrl}/runs/new", new() { WaitUntil = WaitUntilState.NetworkIdle });
    }

    /// <summary>
    /// Clicks a run item in the list panel to select it and load the detail view.
    /// </summary>
    public async Task SelectRunAsync(string runId)
    {
        await RunItem(runId).ClickAsync();
    }

    /// <summary>
    /// Fills in the minimal required fields for the create-run form and submits it.
    /// Waits for redirect to the newly created run's detail page.
    /// </summary>
    public async Task FillCreateFormAsync(string modeKey, string startTime, string? description = null)
    {
        await ModeKeyInput.FillAsync(modeKey);
        await StartTimeInput.FillAsync(startTime);
        if (description is not null)
            await DescriptionInput.FillAsync(description);
    }

    /// <summary>
    /// Fills the description field on the edit-run form and saves.
    /// FluentUI's own Playwright tests fill the inner input directly — the change
    /// event bubbles up to the outer web component where Blazor listens.
    /// </summary>
    public async Task UpdateDescriptionAsync(string description)
    {
        await DescriptionInput.FillAsync(description);
        await SaveChangesButton.ClickAsync();
    }
}
