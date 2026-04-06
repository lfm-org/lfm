using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.Json;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class CreateRunSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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

    private static async Task FillDateTimeGroup(
        ILocator group,
        string month,
        string day,
        string year,
        string hours,
        string minutes)
    {
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Month" }).FillAsync(month);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Day" }).FillAsync(day);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Year" }).FillAsync(year);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Hours" }).FillAsync(hours);
        await group.GetByRole(AriaRole.Spinbutton, new() { Name = "Minutes" }).FillAsync(minutes);
    }

    [Fact]
    public async Task Authenticated_raider_can_create_run_with_modeKey_and_land_on_new_run_card()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs/new");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Create Run" })).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" }).ClickAsync();
        await Expect(_page.GetByText("Instance is required")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Mode is required")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Start time is required")).ToBeVisibleAsync();

        await _page.GetByRole(AriaRole.Combobox).First.ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = "Deadmines" }).ClickAsync();
        await _page.GetByRole(AriaRole.Combobox).Nth(1).ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = "Normal (5 players)" }).ClickAsync();

        await FillDateTimeGroup(
            _page.GetByRole(AriaRole.Group, new() { Name = "Start Time" }),
            month: "03", day: "25", year: "2030", hours: "19", minutes: "30");

        await FillDateTimeGroup(
            _page.GetByRole(AriaRole.Group, new() { Name = "Signup Close Time" }),
            month: "03", day: "25", year: "2030", hours: "18", minutes: "00");

        await _page.GetByLabel("Description").FillAsync("Harness create run");

        // Capture the outgoing POST before clicking submit
        var requestTask = _page.WaitForRequestAsync("**/api/runs");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create Run" }).ClickAsync();
        var request = await requestTask;

        var payload = JsonSerializer.Deserialize<JsonElement>(request.PostData!);
        payload.GetProperty("modeKey").GetString().Should().Be("NORMAL:5");
        payload.TryGetProperty("mode", out _).Should().BeFalse("payload must not include a 'mode' property");

        await Expect(_page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(@"\/runs\?run="));
        var createdRunCard = _page.GetByTestId("run-card").Filter(new() { HasText = "Harness create run" });
        await Expect(createdRunCard).ToBeVisibleAsync();
        await Expect(createdRunCard.GetByText("Normal (5 players)")).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
