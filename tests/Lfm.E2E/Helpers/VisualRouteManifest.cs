// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Seeds;

namespace Lfm.E2E.Helpers;

internal static class VisualRouteManifest
{
    public static readonly IReadOnlyList<VisualViewport> Viewports =
    [
        new("desktop", 1366, 768),
        new("phone", 390, 844),
        new("mobile-floor", 320, 568),
    ];

    public static readonly IReadOnlyList<VisualVariant> Variants =
    [
        VisualVariant.Default,
        VisualVariant.Finnish,
        VisualVariant.Dark,
        VisualVariant.ForcedColorsActive,
    ];

    public static readonly IReadOnlyList<VisualRouteState> States =
    [
        Public("/", "landing", "landing"),
        Public("/login", "login", "login"),
        Public("/privacy", "privacy", "privacy"),
        Public("/login/failed", "login failed", "login-failed"),
        Public("/auth/failure", "auth failure", "auth-failure"),
        Public("/not-found", "not found", "not-found"),
        Public("/goodbye", "goodbye", "goodbye"),

        ProtectedRedirect("/runs", "runs anonymous", "runs-anonymous", "/login?redirect=%2Fruns"),
        Authenticated("/runs", "runs authenticated", "runs-authenticated"),

        ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}", "runs detail anonymous", "runs-detail-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001"),
        Authenticated($"/runs/{DefaultSeed.TestRunId}", "runs detail authenticated", "runs-detail-authenticated"),

        ProtectedRedirect("/runs/new", "runs new anonymous", "runs-new-anonymous", "/login?redirect=%2Fruns%2Fnew"),
        Authenticated("/runs/new", "runs new authenticated", "runs-new-authenticated"),

        ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit anonymous", "runs-edit-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001%2Fedit"),
        Authenticated($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit authenticated", "runs-edit-authenticated"),

        ProtectedRedirect("/characters", "characters anonymous", "characters-anonymous", "/login?redirect=%2Fcharacters"),
        Authenticated("/characters", "characters authenticated", "characters-authenticated"),

        ProtectedRedirect("/guild", "guild anonymous", "guild-anonymous", "/login?redirect=%2Fguild"),
        Authenticated("/guild", "guild authenticated", "guild-authenticated"),

        ProtectedRedirect("/guild/admin", "guild admin anonymous", "guild-admin-anonymous", "/login?redirect=%2Fguild%2Fadmin"),
        SiteAdmin("/guild/admin", "guild admin site admin", "guild-admin-site-admin"),

        ProtectedRedirect("/admin/reference", "admin reference anonymous", "admin-reference-anonymous", "/login?redirect=%2Fadmin%2Freference"),
        SiteAdmin("/admin/reference", "admin reference site admin", "admin-reference-site-admin"),

        ProtectedRedirect("/instances", "instances anonymous", "instances-anonymous", "/login?redirect=%2Finstances"),
        Authenticated("/instances", "instances authenticated", "instances-authenticated"),
    ];

    public static readonly IReadOnlyList<VisualMatrixEntry> Matrix =
        States
            .SelectMany(state => Viewports.SelectMany(viewport =>
                Variants.Select(variant => new VisualMatrixEntry(state, viewport, variant))))
            .ToArray();

    private static VisualRouteState Public(string path, string name, string slug)
        => new(name, path, VisualAccessMode.Public, VisualAnonymousExpectation.Render, null, slug);

    private static VisualRouteState ProtectedRedirect(
        string path,
        string name,
        string slug,
        string expectedAnonymousPathAndQuery)
        => new(
            name,
            path,
            VisualAccessMode.Public,
            VisualAnonymousExpectation.RedirectToLogin,
            expectedAnonymousPathAndQuery,
            slug);

    private static VisualRouteState Authenticated(string path, string name, string slug)
        => new(name, path, VisualAccessMode.Authenticated, VisualAnonymousExpectation.RedirectToLogin, null, slug);

    private static VisualRouteState SiteAdmin(
        string path,
        string name,
        string slug,
        string? expectedAnonymousPathAndQuery = null)
        => new(name, path, VisualAccessMode.SiteAdmin, VisualAnonymousExpectation.RedirectToLogin, expectedAnonymousPathAndQuery, slug);
}

internal sealed record VisualMatrixEntry(
    VisualRouteState State,
    VisualViewport Viewport,
    VisualVariant Variant);
