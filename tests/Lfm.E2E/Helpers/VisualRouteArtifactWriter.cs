// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

internal static class VisualRouteArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<string> CaptureScreenshotAsync(
        string repoRoot,
        IPage page,
        VisualVariant variant,
        VisualViewport viewport,
        VisualRouteState state)
    {
        var screenshotPath = VisualRouteArtifactPaths.ScreenshotAbsolutePath(repoRoot, variant, viewport, state);
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath)!);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true,
        });

        return VisualRouteArtifactPaths.ScreenshotRelativePath(variant, viewport, state);
    }

    public static async Task WriteIndexAsync(
        string repoRoot,
        IReadOnlyCollection<VisualRouteArtifactEntry> entries)
    {
        var indexPath = VisualRouteArtifactPaths.IndexAbsolutePath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);

        var ordered = entries
            .OrderBy(entry => entry.Variant, StringComparer.Ordinal)
            .ThenBy(entry => entry.Viewport, StringComparer.Ordinal)
            .ThenBy(entry => entry.State, StringComparer.Ordinal)
            .ToArray();

        var index = new VisualRouteArtifactIndex(
            Count: ordered.Length,
            Entries: ordered);

        await File.WriteAllTextAsync(indexPath, JsonSerializer.Serialize(index, JsonOptions));
    }

    private sealed record VisualRouteArtifactIndex(
        int Count,
        IReadOnlyCollection<VisualRouteArtifactEntry> Entries);
}
