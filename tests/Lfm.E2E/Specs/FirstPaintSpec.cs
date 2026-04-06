using FluentAssertions;
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

    [Fact]
    public async Task Document_shell_owns_first_paint_before_app_bootstrap()
    {
        // Intercept main entry script so the React app never bootstraps.
        await _page.RouteAsync("**/src/main.tsx", async route =>
        {
            await route.FulfillAsync(new RouteFulfillOptions
            {
                Status = 200,
                ContentType = "application/javascript",
                Body = "",
            });
        });

        await _page.GotoAsync(fixture.AppBaseUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

        var shell = await _page.EvaluateAsync<ShellStyles>("""
            () => ({
                htmlBackground: window.getComputedStyle(document.documentElement).backgroundColor,
                bodyBackground: window.getComputedStyle(document.body).backgroundColor,
                rootDisplay: window.getComputedStyle(document.getElementById('root')).display,
                rootHeight: document.getElementById('root').getBoundingClientRect().height,
                viewportHeight: window.innerHeight,
            })
        """);

        shell.HtmlBackground.Should().Be("rgb(18, 18, 18)");
        shell.BodyBackground.Should().Be("rgb(18, 18, 18)");
        shell.RootDisplay.Should().Be("flex");
        shell.RootHeight.Should().Be(shell.ViewportHeight);
    }

    private sealed record ShellStyles(
        string HtmlBackground,
        string BodyBackground,
        string RootDisplay,
        double RootHeight,
        double ViewportHeight);
}
