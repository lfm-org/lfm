using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class RunsSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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

    // Helpers to find run sidebar buttons — these use partial text matching because button
    // labels include parenthesised player counts which prevent exact Regex-free name lookups.
    private ILocator RunButton(string text) =>
        _page.GetByRole(AriaRole.Button).Filter(new() { HasTextRegex = new Regex(Regex.Escape(text)) });

    [Fact]
    public async Task Authenticated_runs_page_shows_five_full_run_cards_with_pagination()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(RunButton("Deadmines Heroic (5 players)")).ToBeVisibleAsync();
        await Expect(RunButton("Icecrown Citadel Heroic (10 players)")).ToBeVisibleAsync();
        await Expect(RunButton("Deadmines Normal (5 players)")).ToBeVisibleAsync();
        await Expect(RunButton("Icecrown Citadel Heroic (25 players)")).ToBeVisibleAsync();
        await Expect(RunButton("Onyxia's Lair Normal (25 players)")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Rival guild only raid")).ToHaveCountAsync(0);
        await Expect(_page.GetByTestId("run-card")).ToHaveCountAsync(1);
        await Expect(_page.GetByText("Closed heroic cleanup")).ToBeVisibleAsync();

        // Passed section collapsed by default
        await Expect(_page.Locator("#passed-runs-section")).ToHaveCountAsync(0);
        await Expect(_page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex(@"Passed runs \(2\)") })).ToBeVisibleAsync();

        await RunButton("Icecrown Citadel Heroic (10 players)").ClickAsync();
        await Expect(_page.GetByText("Closed progression lockout")).ToBeVisibleAsync();

        await RunButton("Deadmines Normal (5 players)").ClickAsync();
        await Expect(_page.GetByText("Public dungeon warmup")).ToBeVisibleAsync();

        await RunButton("Icecrown Citadel Heroic (25 players)").ClickAsync();
        await Expect(_page.GetByText("Heroic farm night")).ToBeVisibleAsync();

        await RunButton("Onyxia's Lair Normal (25 players)").ClickAsync();
        await Expect(_page.GetByText("Dragon reset clear")).ToBeVisibleAsync();

        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "2", Exact = true })).ToBeVisibleAsync();
        await _page.GetByRole(AriaRole.Button, new() { Name = "2", Exact = true }).ClickAsync();
        await Expect(_page.GetByText("Guild ten-player alt run")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Closed heroic cleanup")).ToHaveCountAsync(0);

        await _page.GetByRole(AriaRole.Button, new() { Name = "1", Exact = true }).ClickAsync();
        await Expect(_page.GetByText("Closed heroic cleanup")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Guild ten-player alt run")).ToHaveCountAsync(0);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await Expect(_page.GetByText("Guild ten-player alt run")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Closed heroic cleanup")).ToHaveCountAsync(0);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Previous" }).ClickAsync();
        await Expect(_page.GetByText("Closed heroic cleanup")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Guild ten-player alt run")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Runs_page_focuses_requested_run_query_on_correct_page()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-guild-dense-molten-core");

        await Expect(_page.GetByText("Guild retro forty-player night")).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "2", Exact = true })).ToHaveAttributeAsync("aria-current", "page");
    }

    [Fact]
    public async Task Passed_runs_section_is_collapsed_by_default_and_expandable()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();

        // Passed section not visible by default
        await Expect(_page.Locator("#passed-runs-section")).ToHaveCountAsync(0);

        // Toggle button shows count
        var toggle = _page.GetByRole(AriaRole.Button)
            .Filter(new() { HasTextRegex = new Regex(@"Passed runs \(2\)") });
        await Expect(toggle).ToBeVisibleAsync();

        // Expand passed section — sidebar shows instance names, not descriptions
        await toggle.ClickAsync();
        var passedSection = _page.Locator("#passed-runs-section");
        await Expect(passedSection).ToBeVisibleAsync();
        await Expect(passedSection.GetByRole(AriaRole.Button)).ToHaveCountAsync(2);

        // Click a passed run to see its description in the detail panel
        await passedSection.GetByRole(AriaRole.Button).First.ClickAsync();
        await Expect(_page.GetByText("Completed heroic speed run")).ToBeVisibleAsync();

        // Collapse again
        await toggle.ClickAsync();
        await Expect(_page.Locator("#passed-runs-section")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Deep_link_to_passed_run_auto_expands_passed_section()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-passed-public-deadmines");
        // Passed section auto-expanded and run selected — description visible in detail panel
        await Expect(_page.Locator("#passed-runs-section")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Completed heroic speed run")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Mobile_runs_page_keeps_cards_compact_until_expanded()
    {
        await _page.SetViewportSizeAsync(390, 844);
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        var heroicFarmCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Heroic farm night" });
        var dragonResetCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Dragon reset clear" });

        await Expect(heroicFarmCard.GetByRole(AriaRole.Button, new() { Name = "Show details" })).ToBeVisibleAsync();
        await Expect(heroicFarmCard.GetByRole(AriaRole.Region, new() { Name = "Your Signup for Heroic farm night" })).ToHaveCountAsync(0);

        await heroicFarmCard.GetByRole(AriaRole.Button, new() { Name = "Show details" }).ClickAsync();
        await dragonResetCard.GetByRole(AriaRole.Button, new() { Name = "Show details" }).ClickAsync();

        await Expect(heroicFarmCard.GetByRole(AriaRole.Region, new() { Name = "Your Signup for Heroic farm night" })).ToBeVisibleAsync();
        await Expect(dragonResetCard.GetByRole(AriaRole.Region, new() { Name = "Your Signup for Dragon reset clear" })).ToBeVisibleAsync();
        await Expect(heroicFarmCard.GetByRole(AriaRole.Button, new() { Name = "Hide details" })).ToBeVisibleAsync();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
