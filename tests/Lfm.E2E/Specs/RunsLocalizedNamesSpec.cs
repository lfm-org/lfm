using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class RunsLocalizedNamesSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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
    public async Task Runs_page_renders_localized_api_names_without_crashing()
    {
        var pageErrors = new List<string>();
        _page.PageError += (_, e) => pageErrors.Add(e);

        await _page.RouteAsync("**/api/runs", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Body = """
                    [
                      {
                        "id": "run-localized",
                        "startTime": "2026-03-25T19:30:00.000Z",
                        "signupCloseTime": "2026-03-25T18:00:00.000Z",
                        "description": "Legacy localized data",
                        "modeKey": "NORMAL:10",
                        "visibility": "PUBLIC",
                        "instanceId": 631,
                        "instanceName": {
                          "en_US": "Icecrown Citadel",
                          "fr_FR": "Citadelle de la Couronne de glace"
                        },
                        "creatorBattleNetId": "test-bnet-id",
                        "creatorGuild": "Test Guild",
                        "createdAt": "2026-03-20T12:00:00.000Z",
                        "raidCharacters": []
                      }
                    ]
                    """,
            });
        });

        await _page.RouteAsync("**/api/instances", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Body = """
                    [
                      {
                        "id": 631,
                        "name": {
                          "en_US": "Icecrown Citadel",
                          "fr_FR": "Citadelle de la Couronne de glace"
                        },
                        "type": "RAID",
                        "minLevel": 80,
                        "expansionId": 3,
                        "modes": [
                          {
                            "mode": {
                              "type": "NORMAL",
                              "name": {
                                "en_US": "Normal",
                                "de_DE": "Normal"
                              }
                            },
                            "players": 10,
                            "is_tracked": true
                          }
                        ]
                      }
                    ]
                    """,
            });
        });

        await _page.RouteAsync("**/api/raider/characters", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Body = """{ "characters": [], "selectedCharacterId": null }""",
            });
        });

        await _page.GotoAsync(fixture.AppBaseUrl + "/runs");

        await Expect(_page.GetByRole(AriaRole.Heading, new() { Name = "Runs" })).ToBeVisibleAsync();
        await Expect(_page.GetByTestId("run-card").GetByRole(AriaRole.Heading, new() { Name = "Icecrown Citadel" })).ToBeVisibleAsync();
        await Expect(_page.GetByTestId("run-card").GetByText("Normal (10 players)")).ToBeVisibleAsync();

        FluentAssertions.AssertionExtensions.Should(pageErrors).BeEmpty();
    }

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
