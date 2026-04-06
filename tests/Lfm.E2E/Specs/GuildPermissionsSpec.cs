using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class GuildPermissionsSpec(DefaultSeedFixture fixture) : IAsyncLifetime
{
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _context = await fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact(Skip = "Blazor CreateRunPage uses FluentSelect for visibility, not Guild/Public buttons; permission-based filtering not yet ported")]
    public async Task Rank_two_guild_members_cannot_create_guild_runs_by_default()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "Blazor app does not implement permission-change flow with login/logout cycle and inline signup region")]
    public async Task Guild_masters_can_change_rank_permissions_and_blocked_ranks_lose_guild_signup_actions()
    {
        await Task.CompletedTask;
    }

    private static IPageAssertions Expect(IPage page) =>
        Microsoft.Playwright.Assertions.Expect(page);

    private static ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
