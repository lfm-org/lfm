// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;

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
        Public("/", "landing", "landing", Heading("Looking For More", "Etsin lisää")),
        Public("/login", "login", "login", Heading("Sign In", "Kirjaudu")),
        Public("/privacy", "privacy", "privacy", Heading("Privacy Policy", "Tietosuojaseloste")),
        Public("/login/failed", "login failed", "login-failed", Heading("Login Failed", "Kirjautuminen epäonnistui")),
        Public("/auth/failure", "auth failure", "auth-failure", Heading("Login Failed", "Kirjautuminen epäonnistui")),
        Public("/not-found", "not found", "not-found", Heading("Not Found", "Ei löytynyt")),
        Public("/goodbye", "goodbye", "goodbye", Heading("Goodbye", "Näkemiin")),

        ProtectedRedirect("/runs", "runs anonymous", "runs-anonymous", "/login?redirect=%2Fruns", Heading("Sign In", "Kirjaudu")),
        Authenticated("/runs", "runs authenticated", "runs-authenticated", RunsListReadyAsync),

        ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}", "runs detail anonymous", "runs-detail-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001", Heading("Sign In", "Kirjaudu")),
        Authenticated($"/runs/{DefaultSeed.TestRunId}", "runs detail authenticated", "runs-detail-authenticated", Heading("Runs", "Runit"), SelectRunAsync),

        ProtectedRedirect("/runs/new", "runs new anonymous", "runs-new-anonymous", "/login?redirect=%2Fruns%2Fnew", Heading("Sign In", "Kirjaudu")),
        Authenticated("/runs/new", "runs new authenticated", "runs-new-authenticated",
            HeadingAndText("Schedule a run", "Uusi runi",
                ("Enter your starting key level before creating this run.", "Anna aloitusavaimen taso ennen runin luomista.")),
            SelectDungeonAsync),

        ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit anonymous", "runs-edit-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001%2Fedit", Heading("Sign In", "Kirjaudu")),
        Authenticated($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit authenticated", "runs-edit-authenticated",
            HeadingAndText("Edit Run", "Muokkaa runia", ("Danger zone", "Vaaravyöhyke"))),

        ProtectedRedirect("/characters", "characters anonymous", "characters-anonymous", "/login?redirect=%2Fcharacters", Heading("Sign In", "Kirjaudu")),
        Authenticated("/characters", "characters authenticated", "characters-authenticated",
            HeadingAndText("My Characters", "Hahmoni", ("Account default character", "Tilin oletushahmo"))),

        ProtectedRedirect("/guild", "guild anonymous", "guild-anonymous", "/login?redirect=%2Fguild", Heading("Sign In", "Kirjaudu")),
        Authenticated("/guild", "guild authenticated", "guild-authenticated",
            HeadingAndText("Guild", "Kilta", ("Guild summary", "Killan yhteenveto"))),

        ProtectedRedirect("/guild/admin", "guild admin anonymous", "guild-admin-anonymous", "/login?redirect=%2Fguild%2Fadmin", Heading("Sign In", "Kirjaudu")),
        SiteAdmin("/guild/admin", "guild admin site admin", "guild-admin-site-admin", Heading("Guild Admin", "Killan hallinta"), LoadGuildAdminAsync),

        ProtectedRedirect("/admin/reference", "admin reference anonymous", "admin-reference-anonymous", "/login?redirect=%2Fadmin%2Freference", Heading("Sign In", "Kirjaudu")),
        SiteAdmin("/admin/reference", "admin reference site admin", "admin-reference-site-admin",
            HeadingAndText("Reference data", "Referenssitiedot",
                ("No reference refresh has run in this session yet.", "Tässä istunnossa ei ole vielä ajettu referenssipäivitystä."))),

        ProtectedRedirect("/instances", "instances anonymous", "instances-anonymous", "/login?redirect=%2Finstances", Heading("Sign In", "Kirjaudu")),
        Authenticated("/instances", "instances authenticated", "instances-authenticated",
            HeadingAndText("Instances", "Instanssit", ("Instance reference", "Instanssireferenssi"))),
    ];

    public static readonly IReadOnlyList<VisualMatrixEntry> Matrix =
        States
            .SelectMany(state => Viewports.SelectMany(viewport =>
                Variants.Select(variant => new VisualMatrixEntry(state, viewport, variant))))
            .ToArray();

    private static Func<IPage, Task> Heading(string english, string finnish)
        => page => Assertions.Expect(page.GetByRole(
                AriaRole.Heading,
                new() { NameRegex = new($"^(?:{Regex.Escape(english)}|{Regex.Escape(finnish)})$") }))
            .ToBeVisibleAsync(new() { Timeout = 15000 });

    private static Func<IPage, Task> HeadingAndText(
        string englishHeading,
        string finnishHeading,
        params (string English, string Finnish)[] texts) =>
        async page =>
        {
            await Heading(englishHeading, finnishHeading)(page);
            foreach (var text in texts)
            {
                await Assertions.Expect(page.GetByText(
                        new Regex($"{Regex.Escape(text.English)}|{Regex.Escape(text.Finnish)}")))
                    .ToBeVisibleAsync(new() { Timeout = 15000 });
            }
        };

    private static async Task RunsListReadyAsync(IPage page)
    {
        await Heading("Runs", "Runit")(page);
        var runsPage = new RunsPage(page);
        await Assertions.Expect(runsPage.RunItem(DefaultSeed.TestRunId))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    private static async Task LoadGuildAdminAsync(IPage page, string apiBaseUrl, string appBaseUrl)
    {
        var guildAdminPage = new GuildAdminPage(page);
        await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
        await Assertions.Expect(guildAdminPage.SloganField).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(page.GetByText(new Regex("Admin guild summary|Hallinnan kiltayhteenveto")))
            .ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    private static async Task SelectRunAsync(IPage page, string apiBaseUrl, string appBaseUrl)
    {
        var runsPage = new RunsPage(page);
        await runsPage.SelectRunAsync(DefaultSeed.TestRunId);
        await Assertions.Expect(runsPage.AttendingHeading).ToBeVisibleAsync(new() { Timeout = 15000 });
    }

    private static async Task SelectDungeonAsync(IPage page, string apiBaseUrl, string appBaseUrl)
    {
        await page.GetByRole(AriaRole.Radio, new() { NameRegex = new("Dungeon|Luolasto") })
            .ClickAsync(new() { Timeout = 15000 });
    }

    private static VisualRouteState Public(
        string path,
        string name,
        string slug,
        Func<IPage, Task> waitForReadyAsync)
        => new(name, path, VisualAccessMode.Public, VisualAnonymousExpectation.Render, null, slug, waitForReadyAsync);

    private static VisualRouteState ProtectedRedirect(
        string path,
        string name,
        string slug,
        string expectedAnonymousPathAndQuery,
        Func<IPage, Task> waitForReadyAsync)
        => new(
            name,
            path,
            VisualAccessMode.Public,
            VisualAnonymousExpectation.RedirectToLogin,
            expectedAnonymousPathAndQuery,
            slug,
            waitForReadyAsync);

    private static VisualRouteState Authenticated(
        string path,
        string name,
        string slug,
        Func<IPage, Task> waitForReadyAsync,
        Func<IPage, string, string, Task>? prepareAsync = null)
        => new(
            name,
            path,
            VisualAccessMode.Authenticated,
            VisualAnonymousExpectation.RedirectToLogin,
            null,
            slug,
            waitForReadyAsync,
            prepareAsync);

    private static VisualRouteState SiteAdmin(
        string path,
        string name,
        string slug,
        Func<IPage, Task> waitForReadyAsync,
        Func<IPage, string, string, Task>? prepareAsync = null)
        => new(
            name,
            path,
            VisualAccessMode.SiteAdmin,
            VisualAnonymousExpectation.RedirectToLogin,
            null,
            slug,
            waitForReadyAsync,
            prepareAsync);
}

internal sealed record VisualMatrixEntry(
    VisualRouteState State,
    VisualViewport Viewport,
    VisualVariant Variant);
