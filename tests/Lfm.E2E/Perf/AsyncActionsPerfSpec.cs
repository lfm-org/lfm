using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Perf;

/// <summary>
/// Perf port of frontend/e2e/perf/async-actions.perf.spec.ts — async action responsiveness.
/// Tagged [Trait("Category", "Perf")] so CI can exclude with --filter "Category!=Perf".
/// Collection: default (full seed data, authenticated context).
/// </summary>
[Collection("default")]
public class AsyncActionsPerfSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Run_signup_completes_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-empty-deadmines");
        var signupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Public dungeon warmup" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Public dungeon warmup" });

        // Wait for characters to load (spinner disappears, attendance buttons visible)
        await signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible });

        var cancelButton = signupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" });

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" }).ClickAsync(),
            ackMarker: cancelButton,
            completionMarker: cancelButton);

        // Verify the signup actually completed
        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" }))
            .ToHaveAttributeAsync("aria-pressed", "true");

        PerfHelper.AssertAcknowledgementWithin(result, CompletionBudget.Network);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    [Fact]
    [Trait("Category", "Perf")]
    public async Task Cancel_signup_completes_within_budget()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-existing-signup-onyxia25");
        var signupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Dragon reset clear" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Dragon reset clear" });

        // Wait for existing signup to render
        await Expect(signupRegion.GetByText("Aelrin")).ToBeVisibleAsync();

        // Enter cancel confirmation
        await signupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        var cancelDialog = _page.GetByRole(AriaRole.Dialog, new() { Name = "Cancel signup?" });
        await cancelDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible });

        // Completion: character select reappears after cancel succeeds
        var characterSelect = signupRegion.GetByLabel("Character");

        var result = await PerfHelper.MeasureInteractionAsync(
            _page,
            () => cancelDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel signup" }).ClickAsync(),
            ackMarker: characterSelect,
            completionMarker: characterSelect);

        // Verify cancel completed — Cancel button should be gone
        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" }))
            .ToHaveCountAsync(0);

        PerfHelper.AssertAcknowledgementWithin(result, CompletionBudget.Network);
        PerfHelper.AssertCompletionWithin(result, CompletionBudget.Network);
        PerfHelper.AssertStableInteraction(result);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
