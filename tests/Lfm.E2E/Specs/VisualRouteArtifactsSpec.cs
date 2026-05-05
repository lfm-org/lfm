// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Lfm.E2E.Specs;

[Collection("VisualArtifacts")]
[Trait("Category", E2ELanes.VisualArtifacts)]
public class VisualRouteArtifactsSpec(VisualArtifactsFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task EveryRouteState_EmitsResponsiveVisualArtifacts()
    {
        var repoRoot = FindRepoRoot();
        var entries = new List<VisualRouteArtifactEntry>();

        foreach (var item in VisualRouteManifest.Matrix)
        {
            var entry = await CaptureMatrixEntryAsync(repoRoot, item);
            entries.Add(entry);
        }

        await VisualRouteArtifactWriter.WriteIndexAsync(repoRoot, entries);

        var failures = entries
            .Where(entry => entry.Status != "captured")
            .Select(entry => $"{entry.Variant}/{entry.Viewport}/{entry.State}: {entry.SkipReason}")
            .ToArray();

        Assert.True(
            failures.Length == 0,
            "Visual route artifact capture failed:\n" + string.Join("\n", failures));
    }

    private async Task<VisualRouteArtifactEntry> CaptureMatrixEntryAsync(
        string repoRoot,
        VisualMatrixEntry item)
    {
        var diagnostics = new VisualDiagnostics();
        IBrowserContext? context = null;
        IPage? page = null;

        try
        {
            context = await CreateContextAsync(item);
            page = await context.NewPageAsync();

            await StubPortraitsAsync(page);
            await AuthenticateIfNeededAsync(page, item.State);
            AttachDiagnostics(page, diagnostics);

            var targetUrl = fixture.Stack.AppBaseUrl + item.State.Path;
            await page.GotoAsync(targetUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

            if (item.State.ExpectedAnonymousPathAndQuery is not null)
            {
                await Assertions.Expect(page).ToHaveURLAsync(
                    new Regex(Regex.Escape(fixture.Stack.AppBaseUrl + item.State.ExpectedAnonymousPathAndQuery) + "$"),
                    new() { Timeout = 15000 });
            }

            await item.State.WaitForReadyAsync(page);

            if (item.State.PrepareAsync is not null)
            {
                await item.State.PrepareAsync(page, fixture.Stack.ApiBaseUrl, fixture.Stack.AppBaseUrl);
                await WaitForVisualReadyAsync(page);
            }

            diagnostics.ThrowIfAny(item);
            await LayoutIntegrityHelper.AssertNoOverlapsAsync(
                page,
                output,
                $"{item.State.Name} [{item.Viewport.Name}, {item.Variant.Name}]");

            var screenshot = await VisualRouteArtifactWriter.CaptureScreenshotAsync(
                repoRoot,
                page,
                item.Variant,
                item.Viewport,
                item.State);

            return ToEntry(item, page.Url, screenshot, "captured", null);
        }
        catch (Exception ex)
        {
            var screenshot = VisualRouteArtifactPaths.ScreenshotRelativePath(item.Variant, item.Viewport, item.State);
            if (page is not null)
            {
                screenshot = await TryCaptureFailureScreenshotAsync(repoRoot, page, item, screenshot);
            }

            return ToEntry(
                item,
                page?.Url ?? fixture.Stack.AppBaseUrl + item.State.Path,
                screenshot,
                "failed",
                ex.Message);
        }
        finally
        {
            if (context is not null)
                await context.CloseAsync();
        }
    }

    private async Task<IBrowserContext> CreateContextAsync(VisualMatrixEntry item)
    {
        var options = new BrowserNewContextOptions
        {
            Locale = item.Variant.Locale,
            ColorScheme = item.Variant.ColorScheme,
            ForcedColors = item.Variant.ForcedColors,
            ViewportSize = new()
            {
                Width = item.Viewport.Width,
                Height = item.Viewport.Height,
            },
        };

        return await fixture.Stack.Browser.NewContextAsync(options);
    }

    private async Task AuthenticateIfNeededAsync(IPage page, VisualRouteState state)
    {
        if (state.AccessMode == VisualAccessMode.Public)
            return;

        var battleNetId = state.AccessMode == VisualAccessMode.SiteAdmin
            ? DefaultSeed.SiteAdminBattleNetId
            : DefaultSeed.PrimaryBattleNetId;

        await AuthHelper.AuthenticatePageAsync(
            page,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId,
            "/");
        await WaitForVisualReadyAsync(page);
    }

    private static void AttachDiagnostics(IPage page, VisualDiagnostics diagnostics)
    {
        page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
                diagnostics.Messages.Enqueue($"[Browser {msg.Type.ToUpperInvariant()}] {msg.Text}");
        };

        page.RequestFailed += (_, req) =>
            diagnostics.Messages.Enqueue($"[Browser REQUESTFAILED] {req.Url} - {req.Failure}");
    }

    private static async Task<string> TryCaptureFailureScreenshotAsync(
        string repoRoot,
        IPage page,
        VisualMatrixEntry item,
        string fallbackScreenshot)
    {
        try
        {
            return await VisualRouteArtifactWriter.CaptureScreenshotAsync(
                repoRoot,
                page,
                item.Variant,
                item.Viewport,
                item.State);
        }
        catch
        {
            return fallbackScreenshot;
        }
    }

    private static async Task WaitForVisualReadyAsync(IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.EvaluateAsync("() => document.fonts ? document.fonts.ready : Promise.resolve()");
    }

    private static async Task StubPortraitsAsync(IPage page)
    {
        await page.RouteAsync("**/api/v1/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Body = "{\"portraits\":{}}",
            });
        });
    }

    private static VisualRouteArtifactEntry ToEntry(
        VisualMatrixEntry item,
        string url,
        string screenshot,
        string status,
        string? skipReason)
        => new(
            Route: item.State.Path,
            State: item.State.Name,
            AccessMode: ToKebab(item.State.AccessMode.ToString()),
            AnonymousExpectation: ToKebab(item.State.AnonymousExpectation.ToString()),
            Viewport: item.Viewport.Name,
            Width: item.Viewport.Width,
            Height: item.Viewport.Height,
            Variant: item.Variant.Name,
            Url: url,
            Screenshot: screenshot,
            Status: status,
            SkipReason: skipReason);

    private static string ToKebab(string value)
        => Regex.Replace(value, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "lfm.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not find lfm.sln walking up from " + AppContext.BaseDirectory);
    }

    private sealed class VisualDiagnostics
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public void ThrowIfAny(VisualMatrixEntry item)
        {
            var messages = Messages
                .Where(message =>
                    !message.Contains("401", StringComparison.OrdinalIgnoreCase) &&
                    !message.Contains("/api/v1/me", StringComparison.OrdinalIgnoreCase) &&
                    !message.Contains("net::ERR_ABORTED", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (messages.Length == 0)
                return;

            throw new XunitException(
                $"Unexpected browser diagnostics for {item.State.Name} [{item.Viewport.Name}, {item.Variant.Name}]:\n" +
                string.Join("\n", messages));
        }
    }
}
