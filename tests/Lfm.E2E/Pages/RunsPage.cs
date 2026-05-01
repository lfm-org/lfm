// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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

    /// <summary>The "Attending (N)" section heading in the run detail panel.</summary>
    public ILocator AttendingHeading =>
        _page.Locator("[data-testid='roster-attending-title']");

    /// <summary>The "Not attending (N)" section heading in the run detail panel.</summary>
    public ILocator NotAttendingHeading =>
        _page.Locator("[data-testid='roster-not-attending-title']");

    /// <summary>
    /// Any character row rendered in the roster sections. Character rows are
    /// class-colored <c>.character-row</c> elements, rendered inside either a
    /// role column (attending) or a full-width list (not attending).
    /// </summary>
    public ILocator RosterCharacterRows =>
        _page.Locator(".character-row");

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

    /// <summary>Mythic+ key level native number input.</summary>
    public ILocator KeyLevelInput =>
        _page.Locator("#keylevel-input");

    /// <summary>Start Time native &lt;input type="datetime-local"&gt; element.</summary>
    public ILocator StartTimeInput =>
        _page.Locator("#starttime-input");

    /// <summary>Signup Close Time native &lt;input type="datetime-local"&gt; element.</summary>
    public ILocator SignupCloseTimeInput =>
        _page.Locator("#signupclose-input");

    /// <summary>Visibility dropdown on the create-run form.</summary>
    public ILocator VisibilitySelect =>
        _page.Locator("#visibility-select").First;

    /// <summary>Description text area web component.</summary>
    public ILocator DescriptionInput =>
        _page.Locator("#description-input");

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
    public async Task FillCreateFormAsync(string keyLevel, string startTime, string? description = null)
    {
        await KeyLevelInput.FillAsync(keyLevel);
        await StartTimeInput.FillAsync(startTime);
        if (description is not null)
            await FillDescriptionAsync(description);
    }

    /// <summary>
    /// Fills the description field on the edit-run form and saves.
    /// FluentUI's own Playwright tests fill the inner input directly — the change
    /// event bubbles up to the outer web component where Blazor listens.
    /// </summary>
    public async Task UpdateDescriptionAsync(string description)
    {
        await FillDescriptionAsync(description);
        await SaveChangesButton.ClickAsync();
    }

    public async Task FillDescriptionAsync(string description)
    {
        await DescriptionInput.EvaluateAsync(
            """
            (element, value) => {
                element.value = value;
                element.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                element.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
            }
            """,
            description);
    }
}
