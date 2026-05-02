// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Helpers;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Lfm.E2E.Specs;

[Trait("Category", "Layout integrity")]
public sealed class LayoutIntegrityHelperSpec(ITestOutputHelper output) : IAsyncLifetime
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        var chromiumExecutablePath = Environment.GetEnvironmentVariable("LFM_E2E_CHROMIUM_PATH");
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ExecutablePath = string.IsNullOrWhiteSpace(chromiumExecutablePath) ? null : chromiumExecutablePath,
        });
        var context = await _browser.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 320, Height = 568 },
        });
        _page = await context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task AssertNoOverlapsAsync_Fails_WhenVisibleInteractiveSiblingsOverlap()
    {
        await Page.SetContentAsync(
            """
            <main>
              <button id="first" style="position:absolute;left:20px;top:20px;width:120px;height:44px">First</button>
              <button id="second" style="position:absolute;left:80px;top:30px;width:120px;height:44px">Second</button>
            </main>
            """);

        var ex = await Assert.ThrowsAsync<XunitException>(
            () => LayoutIntegrityHelper.AssertNoOverlapsAsync(Page, output, "synthetic overlap"));

        Assert.Contains("#first", ex.Message);
        Assert.Contains("#second", ex.Message);
    }

    [Fact]
    public async Task AssertNoOverlapsAsync_AllowsParentChildContainment()
    {
        await Page.SetContentAsync(
            """
            <main>
              <button id="parent" style="width:180px;height:48px">
                <span id="child">Contained label</span>
              </button>
            </main>
            """);

        await LayoutIntegrityHelper.AssertNoOverlapsAsync(Page, output, "synthetic containment");
    }

    [Fact]
    public async Task AssertNoOverlapsAsync_IgnoresHiddenAndPointerlessDecoration()
    {
        await Page.SetContentAsync(
            """
            <main>
              <button id="action" style="position:relative;width:180px;height:48px">Action</button>
              <span id="badge" aria-hidden="true"
                    style="position:absolute;left:130px;top:0;width:64px;height:32px;pointer-events:none">
                Active
              </span>
              <button id="hidden" hidden style="position:absolute;left:20px;top:10px;width:180px;height:48px">
                Hidden
              </button>
            </main>
            """);

        await LayoutIntegrityHelper.AssertNoOverlapsAsync(Page, output, "synthetic decoration");
    }

    [Fact]
    public async Task AssertNoOverlapsAsync_Fails_WhenDocumentHasHorizontalOverflow()
    {
        await Page.SetContentAsync(
            """
            <main style="width:420px">
              <button id="wide">Wide content</button>
            </main>
            """);

        var ex = await Assert.ThrowsAsync<XunitException>(
            () => LayoutIntegrityHelper.AssertNoOverlapsAsync(Page, output, "synthetic overflow"));

        Assert.Contains("horizontal overflow", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private IPage Page => _page ?? throw new InvalidOperationException("Page has not been initialized.");
}
