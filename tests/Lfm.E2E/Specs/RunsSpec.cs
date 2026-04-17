// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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

        // Known limitation: FluentUI <fluent-select> web component does not expose
        // standard ARIA combobox/option roles to Playwright. Prefer GetByRole once
        // upstream support lands (microsoft/fluentui-blazor#2614). Until then, use the
        // element ID set on the component.
        var instanceSelect = Page.Locator("#instance-select");
        await instanceSelect.ClickAsync();
        var firstRealOption = Page.Locator("#instance-select fluent-option").Nth(1);
        await firstRealOption.WaitForAsync(new() { Timeout = 10000 });
        await firstRealOption.ClickAsync();

        // Fill in required form fields with a unique run name via description.
        // Use a future date anchored to UtcNow so the test does not become a
        // time bomb the way the seeded run did (see DefaultSeed.SeedRunAsync).
        var uniqueRunName = $"E2E-Create-{Guid.NewGuid():N}";
        await runsPage.ModeKeyInput.FillAsync("NORMAL:25");
        await runsPage.StartTimeInput.FillAsync(
            DateTimeOffset.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        await runsPage.DescriptionInput.FillAsync(uniqueRunName);

        // Submit
        await runsPage.CreateRunSubmitButton.ClickAsync();

        // Should navigate to the newly created run's detail page. The URL
        // before submit is `/runs/new`, so the regex must exclude that exact
        // slug to avoid matching the pre-submit URL (which would make
        // WaitForURLAsync return immediately).
        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs/(?!new$)[^/]+$"),
            new() { Timeout = 20000 });

        // Capture the newly created run's id from the detail URL.
        // The runs list only displays instance name / mode / date / signup count —
        // not the description — so we must assert by run id, not by the unique
        // description we filled in above.
        var detailUrl = Page.Url;
        var runId = detailUrl.Substring(detailUrl.LastIndexOf('/') + 1);
        runId = Uri.UnescapeDataString(runId);

        // Navigate back to the runs list and verify the new run appears in the list panel.
        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        await Assertions.Expect(runsPage.RunItem(runId)).ToBeVisibleAsync(
            new() { Timeout = 15000 });

        // Click through to the detail panel and verify the unique description is
        // rendered there — this is the end-to-end proof that the body we submitted
        // round-tripped through Cosmos and the runs-detail sanitizer.
        await runsPage.SelectRunAsync(runId);
        await Assertions.Expect(Page.GetByText(uniqueRunName)).ToBeVisibleAsync(
            new() { Timeout = 15000 });
    }

    [Fact]
    public async Task RunDetail_Navigate_ShowsRosterWithSignupCount()
    {
        // The run-detail panel has no coverage UI — only an attendance-grouped
        // roster split into Attending (IN/LATE/BENCH) and Not attending (OUT/AWAY)
        // sections, rendered as CharacterRow components.
        var runsPage = new RunsPage(Page!);

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId)).ToBeVisibleAsync(new() { Timeout = 15000 });

        await runsPage.SelectRunAsync(DefaultSeed.TestRunId);

        // The seeded run has exactly 1 signup ("Aelrin") with reviewedAttendance = IN.
        await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        var attendingText = await runsPage.AttendingHeading.InnerTextAsync();
        Assert.Contains("(1)", attendingText);

        await Assertions.Expect(runsPage.RosterCharacterRows).ToHaveCountAsync(1, new() { Timeout = 10000 });
        await Assertions.Expect(Page!.GetByText("Aelrin")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task RunDetail_DeepLink_DisplaysSeededRoster()
    {
        // The previous name (`SignUp_SelectCharacter_AppearsInRoster`) implied
        // an active signup user journey, but the Blazor UI has no signup-add
        // control: the roster is read-only and signups can only be created via
        // direct API calls. This test actually exercises *deep-link* navigation
        // to a run's detail (vs. RunDetail_Navigate_ShowsRosterWithSignupCount
        // which uses list-click navigation) — a distinct routing path worth
        // covering, but not a signup test. Rename to match what it does.
        var encodedId = Uri.EscapeDataString(DefaultSeed.TestRunId);
        await Page!.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var runsPage = new RunsPage(Page);
        await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(runsPage.RosterCharacterRows).ToHaveCountAsync(1, new() { Timeout = 10000 });

        var attendingText = await runsPage.AttendingHeading.InnerTextAsync();
        Assert.Contains("(1)", attendingText);

        await Assertions.Expect(Page.GetByText("Aelrin")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Fact]
    public async Task EditRunPage_DisplaysSeededRoster()
    {
        // Verifies the edit page renders the roster with seeded character data.
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

        // Log API requests to debug 400 errors
        Page!.Request += (_, req) =>
        {
            if (req.Url.Contains("/api/runs/") && req.Method is "PUT" or "PATCH")
                Log($"[API REQ] {req.Method} {req.Url} body={req.PostData}");
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Url.Contains("/api/runs/") && resp.Status >= 400)
                Log($"[API RESP] {resp.Status} {resp.Url}");
        };

        await Page.GotoAsync(
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

        // Re-read: navigate to the run detail and verify the persisted description
        // round-tripped through Cosmos. The banner alone proves the API returned
        // 200 — it does not prove the value actually persisted, which a future
        // regression that swallows the body would silently break.
        await Page.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}",
            new() { WaitUntil = WaitUntilState.NetworkIdle });
        await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(Page.GetByText(updatedDescription)).ToBeVisibleAsync(
            new() { Timeout = 10000 });
    }

    [Fact]
    public async Task DeleteRun_Confirm_RemovedFromList()
    {
        // This test was previously [Skip]-tagged as flaky; the root cause was the
        // shared `_authContext` used by every other RunsSpec test, which let prior
        // mutations race with the destructive delete flow. Build a dedicated
        // browser context for this test (mirroring AuthSpec.Logout_*) so the
        // mutation runs in isolation from sibling tests.
        var deleteContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var deletePage = await deleteContext.NewPageAsync();

        try
        {
            var runsPage = new RunsPage(deletePage);

            // Create a fresh run via the create-run form so we do not destroy
            // the seeded run used by other tests.
            await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);
            await deletePage.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Select the first available instance. FluentUI custom elements
            // do not yet expose ARIA combobox roles to Playwright (see
            // microsoft/fluentui-blazor#2614), so target by element id.
            var instanceSelect = deletePage.Locator("#instance-select");
            await instanceSelect.ClickAsync();
            var firstRealOption = deletePage.Locator("#instance-select fluent-option").Nth(1);
            await firstRealOption.WaitForAsync(new() { Timeout = 10000 });
            await firstRealOption.ClickAsync();

            var uniqueDescription = $"E2E-Delete-{Guid.NewGuid():N}";
            await runsPage.ModeKeyInput.FillAsync("HEROIC:25");
            await runsPage.StartTimeInput.FillAsync(
                DateTimeOffset.UtcNow.AddDays(60).ToString("yyyy-MM-ddTHH:mm:ssZ"));
            await runsPage.DescriptionInput.FillAsync(uniqueDescription);
            await deletePage.Keyboard.PressAsync("Tab");
            await runsPage.CreateRunSubmitButton.ClickAsync();

            // Wait for navigation to the newly created run's detail. The pre-submit
            // URL is /runs/new — exclude that exact slug to avoid matching it.
            await deletePage.WaitForURLAsync(
                new System.Text.RegularExpressions.Regex(@"/runs/(?!new$)[^/]+$"),
                new() { Timeout = 20000 });

            var detailUrl = deletePage.Url;
            var createdRunId = detailUrl.Substring(detailUrl.LastIndexOf('/') + 1);
            createdRunId = Uri.UnescapeDataString(createdRunId);
            Log($"Created run for delete flow: {createdRunId}");

            await deletePage.GotoAsync(
                $"{fixture.Stack.AppBaseUrl}/runs/{Uri.EscapeDataString(createdRunId)}/edit",
                new() { WaitUntil = WaitUntilState.NetworkIdle });

            await Assertions.Expect(runsPage.DeleteRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });
            await runsPage.DeleteRunButton.ClickAsync();

            await Assertions.Expect(runsPage.ConfirmDeleteButton).ToBeVisibleAsync(new() { Timeout = 10000 });
            await runsPage.ConfirmDeleteButton.ClickAsync();

            await deletePage.WaitForURLAsync(
                new System.Text.RegularExpressions.Regex(@"/runs$"),
                new() { Timeout = 20000 });

            // Wait for the runs list to actually re-render so the absence
            // assertion below is not racing the post-delete reload.
            await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

            await Assertions.Expect(runsPage.RunItem(createdRunId)).Not.ToBeVisibleAsync(
                new() { Timeout = 5000 });
        }
        finally
        {
            await deleteContext.CloseAsync();
        }
    }
}
