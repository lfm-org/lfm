using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class SignupSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Authenticated_raider_can_create_update_and_cancel_a_signup_from_the_combined_runs_page()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-empty-deadmines");
        var signupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Public dungeon warmup" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Public dungeon warmup" });

        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" })).ToBeVisibleAsync();
        await signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" }).ClickAsync();

        await Expect(signupRegion.GetByText("Aelrin")).ToBeVisibleAsync();
        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Late" })).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(signupRegion.GetByRole(AriaRole.Button, new() { Name = "Change character" })).ToBeVisibleAsync();

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs?run=run-public-existing-signup-onyxia25");
        var existingSignupRegion = _page
            .GetByTestId("run-card")
            .Filter(new() { HasText = "Dragon reset clear" })
            .GetByRole(AriaRole.Region, new() { Name = "Your Signup for Dragon reset clear" });

        await Expect(existingSignupRegion.GetByText("Aelrin")).ToBeVisibleAsync();
        await existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Change character" }).ClickAsync();
        await existingSignupRegion.GetByLabel("Character").ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = "Brakka — test-realm" }).ClickAsync();
        await existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Bench" }).ClickAsync();

        await Expect(existingSignupRegion.GetByText("Brakka")).ToBeVisibleAsync();
        await Expect(existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Bench" })).ToHaveAttributeAsync("aria-pressed", "true");

        await existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();
        await existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Yes" }).ClickAsync();
        await Expect(existingSignupRegion.GetByLabel("Character")).ToBeVisibleAsync();
        await Expect(existingSignupRegion.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToHaveCountAsync(0);
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
