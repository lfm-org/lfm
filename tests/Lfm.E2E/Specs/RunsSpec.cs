// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Azure.Cosmos;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Runs")]
[Trait("Category", E2ELanes.Functional)]
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

    // E2E scope: proves the authenticated runs page renders the seeded run list.
    // Cheaper lanes cannot prove this because the browser observes auth, API, storage, and UI composition together.
    // Shared data: read-only.
    [Fact]
    [Trait("Category", E2ELanes.Smoke)]
    public async Task RunsPage_Loads_DisplaysRunList()
    {
        var runsPage = new RunsPage(Page!);

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);

        // Page header is loaded
        await Assertions.Expect(runsPage.CreateRunButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Seed data has at least one run
        await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId)).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    // E2E scope: proves creating a run through the browser appears in list and detail views.
    // Cheaper lanes cannot prove this because form binding, API persistence, routing, and rendering must round-trip.
    // Shared data: disposable.
    [Fact]
    public async Task CreateRun_SubmitForm_AppearsInList()
    {
        var runsPage = new RunsPage(Page!);

        // Navigate to the create-run form
        await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);

        // Wait for the form to load.
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in required form fields with a unique run name via description.
        // Use a future date anchored to UtcNow so the test does not become a
        // time bomb the way the seeded run did (see DefaultSeed.SeedRunAsync).
        // The native <input type="datetime-local"> accepts no timezone suffix;
        // the browser interprets the value as wall-clock local time.
        var uniqueRunName = $"E2E-Create-{Guid.NewGuid():N}";
        await runsPage.KeyLevelInput.FillAsync("10");
        await runsPage.StartTimeInput.FillAsync(
            DateTimeOffset.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ss"));
        await runsPage.FillDescriptionAsync(uniqueRunName);

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
        await Assertions.Expect(runsPage.EditButton).ToBeVisibleAsync(
            new() { Timeout = 10000 });
        await Assertions.Expect(runsPage.NoSignupsMessage).ToBeVisibleAsync(
            new() { Timeout = 10000 });
    }

    // E2E scope: proves list-click navigation renders the seeded run roster and signup count.
    // Cheaper lanes cannot prove this because browser routing and detail-panel rendering compose the final behavior.
    // Shared data: read-only.
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
        await Assertions.Expect(
            runsPage.RosterCharacterRows.GetByText("Aelrin", new() { Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves deep-link navigation renders the seeded run roster.
    // Cheaper lanes cannot prove this because the browser must resolve the route and hydrate the detail panel.
    // Shared data: read-only.
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

        await Assertions.Expect(
            runsPage.RosterCharacterRows.GetByText("Aelrin", new() { Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves the edit route renders the seeded roster in the browser.
    // Cheaper lanes cannot prove this because routed edit-page hydration composes auth, API, and UI state.
    // Shared data: read-only.
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
        await Assertions.Expect(Page.GetByText("Aelrin", new() { Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // E2E scope: proves browser edits to run fields persist and re-render on detail.
    // Cheaper lanes cannot prove this because form binding, API update, storage, and reload must round-trip.
    // Shared data: disposable.
    [Fact]
    public async Task EditRun_ModifyFields_ChangesReflected()
    {
        // Create a dedicated run instead of editing the shared seed. Mutating the
        // seeded description leaves a permanent diff on runs/{TestRunId} that
        // would leak into every subsequent test run against the same database.
        // Mirrors the per-test factory pattern in DeleteRun_Confirm_RemovedFromList.
        Page!.Request += (_, req) =>
        {
            if (req.Url.Contains("/api/v1/runs/") && req.Method is "PUT" or "PATCH")
                Log($"[API REQ] {req.Method} {req.Url} body={req.PostData}");
        };
        Page.Response += (_, resp) =>
        {
            if (resp.Url.Contains("/api/v1/runs/") && resp.Status >= 400)
                Log($"[API RESP] {resp.Status} {resp.Url}");
        };

        var runsPage = new RunsPage(Page);
        var createdRunId = await CreateFreshRunAsync(runsPage);
        var encodedId = Uri.EscapeDataString(createdRunId);

        await Page.GotoAsync(
            $"{fixture.Stack.AppBaseUrl}/runs/{encodedId}/edit",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

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
        await Assertions.Expect(Page.GetByText(updatedDescription)).ToBeVisibleAsync(
            new() { Timeout = 15000 });
    }

    // E2E scope: proves the primary raider can manage their own run signup in the browser.
    // Cheaper lanes cover the individual filters and service outcomes; only E2E proves eligible
    // option rendering, signup persistence, cancellation, and roster re-rendering compose.
    // Shared data: disposable.
    [Fact]
    public async Task Signup_GuildRosteredCharacter_CanManageOwnSignup()
    {
        var page = Page!;
        var runsPage = new RunsPage(page);
        var createdRunId = await SeedDisposableSignupRunAsync();

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(runsPage.RunItem(createdRunId))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await runsPage.SelectRunAsync(createdRunId);

        await Assertions.Expect(runsPage.SignupButton).ToBeVisibleAsync(new() { Timeout = 15000 });

        var optionTexts = await runsPage.SignupCharacterOptionTextsAsync();
        Assert.Contains(optionTexts, text => text.Contains("Aelrin", StringComparison.Ordinal));
        Assert.DoesNotContain(optionTexts, text => text.Contains("Aelrinalt", StringComparison.Ordinal));

        await runsPage.SignupButton.ClickAsync();

        await Assertions.Expect(page.GetByText("Signed up as Aelrin."))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(runsPage.AttendingHeading)
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        var attendingText = await runsPage.AttendingHeading.InnerTextAsync();
        Assert.Contains("(1)", attendingText);

        await Assertions.Expect(
            runsPage.RosterCharacterRows.GetByText("Aelrin", new() { Exact = true }))
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(runsPage.RunItem(createdRunId)).ToBeVisibleAsync(new() { Timeout = 10000 });

        await Assertions.Expect(runsPage.SignupAttendanceOption("In"))
            .ToHaveAttributeAsync("aria-checked", "true", new() { Timeout = 10000 });
        var benchUpdate = page.WaitForResponseAsync(response =>
            response.Url.Contains($"/api/v1/runs/{Uri.EscapeDataString(createdRunId)}/signup", StringComparison.Ordinal)
            && response.Request.Method == "POST"
            && response.Ok);
        await runsPage.SignupAttendanceOption("Bench").ClickAsync();
        await benchUpdate;
        await Assertions.Expect(runsPage.SignupAttendanceOption("Bench"))
            .ToHaveAttributeAsync("aria-checked", "true", new() { Timeout = 15000 });

        await runsPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(runsPage.RunItem(createdRunId)).ToBeVisibleAsync(new() { Timeout = 10000 });
        await runsPage.SelectRunAsync(createdRunId);
        await Assertions.Expect(runsPage.SignupAttendanceOption("Bench"))
            .ToHaveAttributeAsync("aria-checked", "true", new() { Timeout = 15000 });

        await Assertions.Expect(runsPage.CancelSignupButton)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await runsPage.CancelSignupButton.ClickAsync();
        await Assertions.Expect(runsPage.ConfirmCancelSignupButton)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await runsPage.ConfirmCancelSignupButton.ClickAsync();

        await Assertions.Expect(runsPage.SignupButton)
            .ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(runsPage.SignedUpAs("Aelrin"))
            .Not.ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(runsPage.NoSignupsMessage)
            .ToBeVisibleAsync(new() { Timeout = 10000 });
        await Assertions.Expect(runsPage.RosterCharacterRows)
            .ToHaveCountAsync(0, new() { Timeout = 10000 });
    }

    private async Task<string> SeedDisposableSignupRunAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var startTime = now.AddDays(21);
        var signupCloseTime = startTime.AddMinutes(-30);
        var createdAt = now.AddMinutes(-5);
        var runId = $"e2e-signup-{Guid.NewGuid():N}";
        const string Format = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        var run = new Dictionary<string, object?>
        {
            ["id"] = runId,
            ["startTime"] = startTime.ToString(Format),
            ["signupCloseTime"] = signupCloseTime.ToString(Format),
            ["description"] = "E2E primary signup management",
            ["modeKey"] = "NORMAL:25",
            ["visibility"] = "GUILD",
            ["creatorGuild"] = "Test Guild",
            ["creatorGuildId"] = 12345,
            ["instanceId"] = 67,
            ["instanceName"] = "Liberation of Undermine",
            ["creatorBattleNetId"] = DefaultSeed.PrimaryBattleNetId,
            ["createdAt"] = createdAt.ToString(Format),
            ["ttl"] = 2592000,
            ["runCharacters"] = new List<object>(),
        };

        var container = fixture.Stack.CosmosClient.GetContainer(StackFixture.DatabaseName, "runs");
        await container.UpsertItemAsync(run, new PartitionKey(runId));
        return runId;
    }

    /// <summary>
    /// Creates a fresh run via the create-run form and returns the new run id.
    /// Callers use this to scope destructive mutations to a per-test document so
    /// no test bleeds state into <c>runs/{DefaultSeed.TestRunId}</c>.
    /// </summary>
    private async Task<string> CreateFreshRunAsync(RunsPage runsPage)
    {
        await runsPage.NavigateToCreateRunAsync(fixture.Stack.AppBaseUrl);
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await runsPage.KeyLevelInput.FillAsync("10");
        // Native <input type="datetime-local"> rejects a Z suffix.
        await runsPage.StartTimeInput.FillAsync(
            DateTimeOffset.UtcNow.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ss"));
        await runsPage.FillDescriptionAsync($"E2E-Scratch-{Guid.NewGuid():N}");

        await runsPage.CreateRunSubmitButton.ClickAsync();

        // The pre-submit URL is /runs/new — exclude it from the match.
        await Page.WaitForURLAsync(
            new System.Text.RegularExpressions.Regex(@"/runs/(?!new$)[^/]+$"),
            new() { Timeout = 20000 });

        var detailUrl = Page.Url;
        var runId = detailUrl.Substring(detailUrl.LastIndexOf('/') + 1);
        return Uri.UnescapeDataString(runId);
    }

    // E2E scope: proves browser deletion removes a created run from the rendered list.
    // Cheaper lanes cannot prove this because confirmation UI, API delete, routing, and list refresh must compose.
    // Shared data: disposable.
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

            var uniqueDescription = $"E2E-Delete-{Guid.NewGuid():N}";
            await runsPage.KeyLevelInput.FillAsync("10");
            // Native <input type="datetime-local"> rejects a Z suffix; the
            // browser interprets the value as wall-clock local time.
            await runsPage.StartTimeInput.FillAsync(
                DateTimeOffset.UtcNow.AddDays(60).ToString("yyyy-MM-ddTHH:mm:ss"));
            await runsPage.FillDescriptionAsync(uniqueDescription);
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
            await Assertions.Expect(runsPage.DeleteCancelButton).ToBeVisibleAsync(new() { Timeout = 10000 });
            await runsPage.DeleteCancelButton.ClickAsync();

            await Assertions.Expect(runsPage.ConfirmDeleteButton).Not.ToBeVisibleAsync(
                new() { Timeout = 10000 });
            await Assertions.Expect(runsPage.DeleteRunButton).ToBeVisibleAsync(new() { Timeout = 10000 });

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
