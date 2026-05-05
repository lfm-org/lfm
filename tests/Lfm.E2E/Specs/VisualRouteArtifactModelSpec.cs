// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Specs;

[Trait("Category", E2ELanes.VisualArtifacts)]
public class VisualRouteArtifactModelSpec
{
    [Fact]
    public void VisualArtifactsLane_IsDeclared()
    {
        Assert.Equal("VisualArtifacts", E2ELanes.VisualArtifacts);
    }

    [Fact]
    public void ArtifactPath_UsesVariantViewportAndRouteSlug()
    {
        var viewport = new VisualViewport("mobile-floor", 320, 568);
        var variant = VisualVariant.ForcedColorsActive;
        var state = new VisualRouteState(
            "runs detail authenticated",
            "/runs/e2e-run-001",
            VisualAccessMode.Authenticated,
            VisualAnonymousExpectation.RedirectToLogin,
            "/login?redirect=%2Fruns%2Fe2e-run-001",
            "runs-detail-authenticated");

        var relativePath = VisualRouteArtifactPaths.ScreenshotRelativePath(variant, viewport, state);

        Assert.Equal("visual-routes/forced-colors/mobile-floor/runs-detail-authenticated.png", relativePath);
    }

    [Fact]
    public void ArtifactEntry_RecordsInspectableMetadata()
    {
        var entry = new VisualRouteArtifactEntry(
            Route: "/guild/admin",
            State: "guild admin site admin",
            AccessMode: "site-admin",
            AnonymousExpectation: "redirect-to-login",
            Viewport: "desktop",
            Width: 1366,
            Height: 768,
            Variant: "dark",
            Url: "http://localhost/guild/admin",
            Screenshot: "visual-routes/dark/desktop/guild-admin-site-admin.png",
            Status: "captured",
            SkipReason: null);

        Assert.Equal("site-admin", entry.AccessMode);
        Assert.Equal("captured", entry.Status);
        Assert.Null(entry.SkipReason);
    }

    [Fact]
    public void Manifest_CoversEveryApprovedRouteState()
    {
        Assert.Equal(25, VisualRouteManifest.States.Count);
        Assert.Equal(3, VisualRouteManifest.Viewports.Count);
        Assert.Equal(4, VisualRouteManifest.Variants.Count);
        Assert.Equal(300, VisualRouteManifest.Matrix.Count);
    }

    [Fact]
    public void Manifest_IncludesProtectedAnonymousAndAuthorizedStates()
    {
        Assert.Contains(VisualRouteManifest.States, state =>
            state.Path == "/runs" &&
            state.AccessMode == VisualAccessMode.Public &&
            state.AnonymousExpectation == VisualAnonymousExpectation.RedirectToLogin &&
            state.ExpectedAnonymousPathAndQuery == "/login?redirect=%2Fruns");

        Assert.Contains(VisualRouteManifest.States, state =>
            state.Path == "/runs" &&
            state.AccessMode == VisualAccessMode.Authenticated &&
            state.AnonymousExpectation == VisualAnonymousExpectation.RedirectToLogin);

        Assert.Contains(VisualRouteManifest.States, state =>
            state.Path == "/admin/reference" &&
            state.AccessMode == VisualAccessMode.Public &&
            state.AnonymousExpectation == VisualAnonymousExpectation.RedirectToLogin &&
            state.ExpectedAnonymousPathAndQuery == "/login?redirect=%2Fadmin%2Freference");
    }

    [Fact]
    public async Task ArtifactWriter_WritesDeterministicIndexJson()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "lfm-visual-artifact-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);

        try
        {
            var entries = new[]
            {
                new VisualRouteArtifactEntry(
                    Route: "/",
                    State: "landing",
                    AccessMode: "public",
                    AnonymousExpectation: "render",
                    Viewport: "desktop",
                    Width: 1366,
                    Height: 768,
                    Variant: "default",
                    Url: "http://localhost/",
                    Screenshot: "visual-routes/default/desktop/landing.png",
                    Status: "captured",
                    SkipReason: null),
            };

            await VisualRouteArtifactWriter.WriteIndexAsync(outputRoot, entries);

            var indexPath = Path.Combine(outputRoot, "artifacts", "e2e-results", "visual-routes", "index.json");
            var json = await File.ReadAllTextAsync(indexPath);
            Assert.Contains("\"route\": \"/\"", json);
            Assert.Contains("\"status\": \"captured\"", json);
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }
}
