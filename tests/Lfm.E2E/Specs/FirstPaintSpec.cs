using Lfm.E2E.Fixtures;
using Microsoft.Playwright;
using Xunit;

namespace Lfm.E2E.Specs;

[Collection("default")]
public class FirstPaintSpec(DefaultSeedFixture fixture) : IAsyncLifetime
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

    [Fact(Skip = "Blazor WASM first-paint is handled by _framework/blazor.webassembly.js, not src/main.tsx; shell styling differs from React version")]
    public async Task Document_shell_owns_first_paint_before_app_bootstrap()
    {
        await Task.CompletedTask;
    }
}
