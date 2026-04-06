using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port of frontend/e2e/perf/forms.perf.spec.ts — form responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class FormsPerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    [Trait("Category", "Perf")]
    public async Task Create_run_page_loads_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Plan runs in one place" }))
            .ToBeVisibleAsync();

        var main = _page.GetByRole(AriaRole.Main);
        var heading = _page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(
                fixture.AppBaseUrl + "/runs/new",
                new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: heading);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Validation_errors_appear_within_budget_on_empty_submit()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();

        var submitButton = _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });
        var validationError = _page.GetByText("Instance is required");

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => submitButton.ClickAsync(),
            ackMarker: validationError,
            completionMarker: validationError);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Standard);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Create_run_submit_completes_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();

        // Fill the form
        await _page.GetByRole(AriaRole.Combobox).First.ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = "Deadmines" }).ClickAsync();
        await _page.GetByRole(AriaRole.Combobox).Nth(1).ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = "Normal (5 players)" }).ClickAsync();
        await FillDateTimeGroupAsync(
            _page.GetByRole(AriaRole.Group, new() { Name = "Start Time" }),
            month: "12", day: "25", year: "2030", hours: "19", minutes: "30");
        await FillDateTimeGroupAsync(
            _page.GetByRole(AriaRole.Group, new() { Name = "Signup Close Time" }),
            month: "12", day: "25", year: "2030", hours: "18", minutes: "00");
        await _page.GetByLabel("Description").FillAsync("Perf test run");

        var submitButton = _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" });
        var createdCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Perf test run" });

        // The local test backend redirects fast enough that the transient submit
        // spinner is not a durable marker. Use the first stable post-submit state.
        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => submitButton.ClickAsync(),
            ackMarker: createdCard,
            completionMarker: createdCard);

        // Verify redirect happened
        await ExpectPage(_page).ToHaveURLAsync(new Regex(@"\/runs\?run="));

        PerfHelper.AssertAcknowledgementWithin(result, CompletionBudget.Network);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    private static async Task FillDateTimeGroupAsync(
        ILocator group,
        string month, string day, string year, string hours, string minutes)
    {
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Month" }).FillAsync(month);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Day" }).FillAsync(day);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Year" }).FillAsync(year);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Hours" }).FillAsync(hours);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Minutes" }).FillAsync(minutes);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);

    private static IPageAssertions ExpectPage(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);
}
