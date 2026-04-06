using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildHomeSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Authenticated_guild_members_can_open_the_read_only_guild_home()
    {
        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        await _page.GetByRole(AriaRole.Link, new() { Name = "Guild" }).ClickAsync();

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Guild", Exact = true })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Test Guild" })).ToBeVisibleAsync();
        var crest = _page.GetByRole(AriaRole.Img, new() { Name = "Test Guild crest" });
        await Expect(crest).ToBeVisibleAsync();
        await Expect(crest).ToHaveAttributeAsync("src", new Regex(@"\/api\/guild\/12345\/crest$"));
        await Expect(_page.GetByText("Read-only")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Time zone")).ToHaveCountAsync(0);
        await Expect(_page.GetByLabel("Slogan")).ToHaveCountAsync(0);
        await Expect(_page.GetByText("Guild run creation blocked for your rank")).ToBeVisibleAsync();
        await Expect(_page.GetByText("You can sign up to guild runs")).ToBeVisibleAsync();
        await Expect(_page.GetByText("Rank permissions and crest customization land in the next slices.")).ToHaveCountAsync(0);

        var response = await _context.APIRequest.GetAsync(fixture.ApiBaseUrl + "/api/guild/12345/crest");
        response.Ok.Should().BeTrue();
        response.Headers["content-type"].Should().Contain("image/svg+xml");

        var renderedCenterPixel = await crest.EvaluateAsync<int[]>(
            @"async (img) => {
                await img.decode();
                const canvas = document.createElement('canvas');
                canvas.width = img.naturalWidth;
                canvas.height = img.naturalHeight;
                const context = canvas.getContext('2d');
                if (!context) throw new Error('2d context unavailable');
                context.drawImage(img, 0, 0);
                return Array.from(
                    context.getImageData(
                        Math.floor(img.naturalWidth / 2),
                        Math.floor(img.naturalHeight / 2),
                        1,
                        1
                    ).data
                );
            }");
        renderedCenterPixel.Should().NotEqual(new[] { 158, 0, 54, 255 });
    }

    [Fact]
    public async Task Guild_masters_can_save_slogan_and_timezone_before_entering_raids()
    {
        await _page.GotoAsync(
            fixture.ApiBaseUrl + "/api/battlenet/login?redirect=%2Fruns&testAuthScenario=guild-master");

        await Expect(_page).ToHaveURLAsync(new Regex(@"\/guild$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Guild", Exact = true })).ToBeVisibleAsync();
        await Expect(_page.GetByText("Guild master setup required")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Time zone")).ToHaveValueAsync("Europe/Helsinki");
        await Expect(_page.GetByLabel("Slogan")).ToHaveValueAsync("");

        await _page.GetByLabel("Slogan").FillAsync("Raid nights, less scrollback.");
        await _page.GetByLabel("Time zone").SelectOptionAsync("America/New_York");
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save guild settings" }).ClickAsync();

        await Expect(_page.GetByText("Settings live")).ToBeVisibleAsync();
        await Expect(_page.GetByLabel("Slogan")).ToHaveValueAsync("Raid nights, less scrollback.");
        await Expect(_page.GetByLabel("Time zone")).ToHaveValueAsync("America/New_York");
        await Expect(_page.GetByText("Rank permissions and crest customization land in the next slices.")).ToHaveCountAsync(0);

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");
        await Expect(_page).ToHaveURLAsync(new Regex(@"\/runs$"));
        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
