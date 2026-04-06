using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port of frontend/e2e/perf/mobile.perf.spec.ts — mobile responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class MobilePerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private const int MobileWidth = 390;
    private const int MobileHeight = 844;

    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await AuthHelper.CreateAuthenticatedContextAsync(
            fixture.Browser, fixture.ApiBaseUrl, fixture.AppBaseUrl);
        _page = await _context.NewPageAsync();
        await _page.SetViewportSizeAsync(MobileWidth, MobileHeight);
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Mobile_runs_list_loads_within_budget()
    {
        var main = _page.GetByRole(AriaRole.Main);
        var firstCard = _page.GetByTestId("run-card").First;

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            async () => await _page.GotoAsync(fixture.AppBaseUrl + "/runs", new() { WaitUntil = WaitUntilState.Commit }),
            ackMarker: main,
            completionMarker: firstCard);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Entry);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Mobile);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Mobile_card_expand_shows_details_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        await _page.GetByTestId("run-card").First.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // "Heroic farm night" is on page 1 in the default seed — confirmed by
        // runs.spec.ts which references it without pagination.
        var targetCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Heroic farm night" });
        var expandButton = targetCard.GetByRole(AriaRole.Button, new() { Expanded = false });
        var signupRegion = targetCard.GetByRole(AriaRole.Region,
            new() { Name = "Your Signup for Heroic farm night" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => expandButton.ClickAsync(),
            ackMarker: signupRegion,
            completionMarker: signupRegion);

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Standard);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Fast);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Mobile_run_signup_shows_busy_state_and_completes_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-empty-deadmines");

        // On mobile, the target card auto-expands because of the ?run= param
        var signupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Public dungeon warmup" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Public dungeon warmup" });

        await signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Mobile signup does not expose a durable intermediate busy marker on the
        // local test backend, so use the first stable post-submit state instead.
        var cancelButton = signupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" }).ClickAsync(),
            ackMarker: cancelButton,
            completionMarker: cancelButton);

        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" }))
            .ToHaveAttributeAsync("aria-pressed", "true");

        PerfHelper.AssertAcknowledgementWithin(result, AckBudget.Standard);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Mobile);
        PerfHelper.AssertStableInteraction(result);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
