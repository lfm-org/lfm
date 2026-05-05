// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

internal enum VisualAccessMode
{
    Public,
    Authenticated,
    SiteAdmin,
}

internal enum VisualAnonymousExpectation
{
    Render,
    RedirectToLogin,
}

internal sealed record VisualViewport(string Name, int Width, int Height);

internal sealed record VisualVariant(
    string Name,
    string? Locale = null,
    ColorScheme? ColorScheme = null,
    ForcedColors? ForcedColors = null)
{
    public static readonly VisualVariant Default = new("default");
    public static readonly VisualVariant Finnish = new("fi", Locale: "fi-FI");
    public static readonly VisualVariant Dark = new("dark", ColorScheme: Microsoft.Playwright.ColorScheme.Dark);
    public static readonly VisualVariant ForcedColorsActive = new("forced-colors", ForcedColors: Microsoft.Playwright.ForcedColors.Active);
}

internal sealed record VisualRouteState(
    string Name,
    string Path,
    VisualAccessMode AccessMode,
    VisualAnonymousExpectation AnonymousExpectation,
    string? ExpectedAnonymousPathAndQuery,
    string Slug,
    Func<IPage, Task> WaitForReadyAsync,
    Func<IPage, string, string, Task>? PrepareAsync = null);

internal sealed record VisualRouteArtifactEntry(
    string Route,
    string State,
    string AccessMode,
    string AnonymousExpectation,
    string Viewport,
    int Width,
    int Height,
    string Variant,
    string Url,
    string Screenshot,
    string Status,
    string? SkipReason);

internal static class VisualRouteArtifactPaths
{
    public const string Root = "artifacts/e2e-results/visual-routes";

    public static string ScreenshotRelativePath(
        VisualVariant variant,
        VisualViewport viewport,
        VisualRouteState state)
        => Path.Combine(
            "visual-routes",
            variant.Name,
            viewport.Name,
            state.Slug + ".png")
            .Replace(Path.DirectorySeparatorChar, '/');

    public static string ScreenshotAbsolutePath(
        string repoRoot,
        VisualVariant variant,
        VisualViewport viewport,
        VisualRouteState state)
        => Path.Combine(
            repoRoot,
            "artifacts",
            "e2e-results",
            "visual-routes",
            variant.Name,
            viewport.Name,
            state.Slug + ".png");

    public static string IndexAbsolutePath(string repoRoot)
        => Path.Combine(repoRoot, "artifacts", "e2e-results", "visual-routes", "index.json");
}
