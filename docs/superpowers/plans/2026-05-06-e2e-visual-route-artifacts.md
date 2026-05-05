# E2E Visual Route Artifacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Playwright .NET E2E lane that captures local screenshot artifacts for every Blazor route across desktop, phone, mobile-floor, locale, dark-mode, and forced-colors states while failing on browser-observable defects.

**Architecture:** Add a dedicated `VisualArtifacts` E2E lane with its own fixture, route manifest, capture helper, and spec. Keep route-state metadata and artifact naming in focused helpers so the spec stays orchestration-only. Reuse `AuthHelper`, `DefaultSeed`, and `LayoutIntegrityHelper`; do not add pixel baselines.

**Tech Stack:** .NET 10, xUnit, Microsoft.Playwright 1.59.0, existing Testcontainers-backed `tests/Lfm.E2E` stack.

---

## File Structure

- Modify: `tests/Lfm.E2E/Infrastructure/E2ETestMetadata.cs`
  - Add the `VisualArtifacts` lane constant.
- Create: `tests/Lfm.E2E/Fixtures/VisualArtifactsFixture.cs`
  - Own the xUnit collection for the new lane and reuse `SharedStack`.
- Create: `tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs`
  - Define route-state, viewport, variant, and artifact-index records plus deterministic slug/path helpers.
- Create: `tests/Lfm.E2E/Helpers/VisualRouteManifest.cs`
  - Define the route/state manifest and route-specific ready/setup delegates.
- Create: `tests/Lfm.E2E/Helpers/VisualRouteArtifactWriter.cs`
  - Write full-page screenshots and `index.json`.
- Create: `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`
  - Fast non-Docker tests for manifest size, naming, and output paths.
- Create: `tests/Lfm.E2E/Specs/VisualRouteArtifactsSpec.cs`
  - Execute the full browser matrix and emit artifacts.

Keep all new code in `tests/Lfm.E2E`; there are no app, API, contract, or infrastructure changes.

---

### Task 1: Lane Metadata And Artifact Model

**Files:**
- Modify: `tests/Lfm.E2E/Infrastructure/E2ETestMetadata.cs`
- Create: `tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs`
- Create: `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`

- [ ] **Step 1: Write the failing metadata/model tests**

Create `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`:

```csharp
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
}
```

- [ ] **Step 2: Run the new test and verify it fails**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: FAIL at compile time because `E2ELanes.VisualArtifacts`, `VisualViewport`, `VisualVariant`, `VisualRouteState`, `VisualAccessMode`, `VisualAnonymousExpectation`, `VisualRouteArtifactPaths`, and `VisualRouteArtifactEntry` do not exist.

- [ ] **Step 3: Add the lane constant**

Modify `tests/Lfm.E2E/Infrastructure/E2ETestMetadata.cs` by adding:

```csharp
public const string VisualArtifacts = "VisualArtifacts";
```

Keep it inside `public static class E2ELanes`, after `LayoutIntegrity` and before `Security`.

- [ ] **Step 4: Add the artifact model**

Create `tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs`:

```csharp
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
    string Slug);

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
```

- [ ] **Step 5: Run the model tests and verify they pass**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: PASS for `VisualRouteArtifactModelSpec`.

- [ ] **Step 6: Commit Task 1**

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E/Infrastructure/E2ETestMetadata.cs tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs
git -C /home/souroldgeezer/repos/lfm commit -m "Add visual artifact model"
```

---

### Task 2: Route Manifest And Fixture

**Files:**
- Create: `tests/Lfm.E2E/Fixtures/VisualArtifactsFixture.cs`
- Create: `tests/Lfm.E2E/Helpers/VisualRouteManifest.cs`
- Modify: `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`

- [ ] **Step 1: Extend the manifest tests before implementing the manifest**

Append these tests to `VisualRouteArtifactModelSpec`:

```csharp
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
```

- [ ] **Step 2: Run the manifest tests and verify they fail**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: FAIL at compile time because `VisualRouteManifest` does not exist.

- [ ] **Step 3: Add the visual artifacts fixture**

Create `tests/Lfm.E2E/Fixtures/VisualArtifactsFixture.cs`:

```csharp
// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Infrastructure;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("VisualArtifacts")]
public class VisualArtifactsCollection : ICollectionFixture<VisualArtifactsFixture> { }

public class VisualArtifactsFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

- [ ] **Step 4: Add the route manifest**

Create `tests/Lfm.E2E/Helpers/VisualRouteManifest.cs`:

```csharp
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

    private static VisualRouteState SiteAdmin(string path, string name, string slug)
        => new(name, path, VisualAccessMode.SiteAdmin, VisualAnonymousExpectation.RedirectToLogin, null, slug);
}

internal sealed record VisualMatrixEntry(
    VisualRouteState State,
    VisualViewport Viewport,
    VisualVariant Variant);
```

- [ ] **Step 5: Run the manifest tests and verify they pass**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: PASS. The manifest count must be exactly `25` states and `300` matrix entries.

- [ ] **Step 6: Commit Task 2**

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E/Fixtures/VisualArtifactsFixture.cs tests/Lfm.E2E/Helpers/VisualRouteManifest.cs tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs
git -C /home/souroldgeezer/repos/lfm commit -m "Add visual route manifest"
```

---

### Task 3: Artifact Writer

**Files:**
- Create: `tests/Lfm.E2E/Helpers/VisualRouteArtifactWriter.cs`
- Modify: `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`

- [ ] **Step 1: Write failing writer tests**

Ensure `VisualRouteArtifactModelSpec` has this using directive:

```csharp
using System.Text.Json;
```

Append these tests to `VisualRouteArtifactModelSpec`:

```csharp
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
                Route: "/guild/admin",
                State: "guild admin site admin",
                AccessMode: "site-admin",
                AnonymousExpectation: "redirect-to-login",
                Viewport: "mobile-floor",
                Width: 320,
                Height: 568,
                Variant: "forced-colors",
                Url: "http://localhost/guild/admin",
                Screenshot: "visual-routes/forced-colors/mobile-floor/guild-admin-site-admin.png",
                Status: "captured",
                SkipReason: "not skipped"),
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
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("count").GetInt32());
        Assert.False(root.TryGetProperty("generatedAtUtc", out _));

        var entriesJson = root.GetProperty("entries").EnumerateArray().ToArray();
        Assert.Equal("default", entriesJson[0].GetProperty("variant").GetString());
        Assert.Equal("desktop", entriesJson[0].GetProperty("viewport").GetString());
        Assert.Equal("landing", entriesJson[0].GetProperty("state").GetString());
        Assert.False(entriesJson[0].TryGetProperty("skipReason", out _));
        Assert.Equal("forced-colors", entriesJson[1].GetProperty("variant").GetString());
        Assert.Equal("mobile-floor", entriesJson[1].GetProperty("viewport").GetString());
        Assert.Equal("guild admin site admin", entriesJson[1].GetProperty("state").GetString());
        Assert.Equal("not skipped", entriesJson[1].GetProperty("skipReason").GetString());
    }
    finally
    {
        Directory.Delete(outputRoot, recursive: true);
    }
}
```

- [ ] **Step 2: Run the writer tests and verify they fail**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: FAIL at compile time because `VisualRouteArtifactWriter` does not exist.

- [ ] **Step 3: Add the writer**

Create `tests/Lfm.E2E/Helpers/VisualRouteArtifactWriter.cs`:

```csharp
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
```

- [ ] **Step 4: Run the writer tests and verify they pass**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: PASS for all model, manifest, and writer tests.

- [ ] **Step 5: Commit Task 3**

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E/Helpers/VisualRouteArtifactWriter.cs tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs
git -C /home/souroldgeezer/repos/lfm commit -m "Add visual artifact writer"
```

---

### Task 4: Browser Matrix Spec

**Files:**
- Create: `tests/Lfm.E2E/Specs/VisualRouteArtifactsSpec.cs`
- Modify: `tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs`
- Modify: `tests/Lfm.E2E/Helpers/VisualRouteManifest.cs`
- Modify: `tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs`

- [ ] **Step 1: Add browser-ready delegates to the model**

Modify `VisualRouteState` in `VisualRouteArtifactModel.cs` to include route-specific browser behavior:

```csharp
internal sealed record VisualRouteState(
    string Name,
    string Path,
    VisualAccessMode AccessMode,
    VisualAnonymousExpectation AnonymousExpectation,
    string? ExpectedAnonymousPathAndQuery,
    string Slug,
    Func<IPage, Task> WaitForReadyAsync,
    Func<IPage, string, string, Task>? PrepareAsync = null);
```

Expected compile result before manifest update: FAIL because the existing manifest constructors no longer pass `WaitForReadyAsync`.
Update any direct `VisualRouteState` construction in `VisualRouteArtifactModelSpec` to pass `_ => Task.CompletedTask` as the ready delegate so those tests stay focused on artifact path/model behavior.

- [ ] **Step 2: Update the manifest with ready checks and setup delegates**

Modify `VisualRouteManifest.cs` so every route state passes a ready delegate. Add these helper methods inside `VisualRouteManifest`:

```csharp
private static Func<IPage, Task> Heading(string english, string finnish)
    => page => Assertions.Expect(page.GetByRole(
            AriaRole.Heading,
            new() { NameRegex = new($"{Regex.Escape(english)}|{Regex.Escape(finnish)}") }))
        .ToBeVisibleAsync(new() { Timeout = 15000 });

private static Func<IPage, Task> Button(string english, string finnish)
    => page => Assertions.Expect(page.GetByRole(
            AriaRole.Button,
            new() { NameRegex = new($"{Regex.Escape(english)}|{Regex.Escape(finnish)}") }))
        .ToBeVisibleAsync(new() { Timeout = 15000 });

private static async Task LoadGuildAdminAsync(IPage page, string apiBaseUrl, string appBaseUrl)
{
    var guildAdminPage = new GuildAdminPage(page);
    await guildAdminPage.LoadGuildAsync(DefaultSeed.TestGuildId);
    await Assertions.Expect(guildAdminPage.SloganField).ToBeVisibleAsync(new() { Timeout = 15000 });
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
```

Add these `using` directives:

```csharp
using System.Text.RegularExpressions;
using Lfm.E2E.Pages;
using Microsoft.Playwright;
```

Use these ready checks in the manifest:

```csharp
Public("/", "landing", "landing", Heading("Looking For More", "Etsin lisää")),
Public("/login", "login", "login", Heading("Sign In", "Kirjaudu")),
Public("/privacy", "privacy", "privacy", Heading("Privacy Policy", "Tietosuojaseloste")),
Public("/login/failed", "login failed", "login-failed", Heading("Login Failed", "Kirjautuminen epäonnistui")),
Public("/auth/failure", "auth failure", "auth-failure", Heading("Login Failed", "Kirjautuminen epäonnistui")),
Public("/not-found", "not found", "not-found", Heading("Not Found", "Ei löytynyt")),
Public("/goodbye", "goodbye", "goodbye", Heading("Goodbye", "Näkemiin")),

ProtectedRedirect("/runs", "runs anonymous", "runs-anonymous", "/login?redirect=%2Fruns", Heading("Sign In", "Kirjaudu")),
Authenticated("/runs", "runs authenticated", "runs-authenticated", Heading("Runs", "Runit")),

ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}", "runs detail anonymous", "runs-detail-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001", Heading("Sign In", "Kirjaudu")),
Authenticated($"/runs/{DefaultSeed.TestRunId}", "runs detail authenticated", "runs-detail-authenticated", Heading("Runs", "Runit"), SelectRunAsync),

ProtectedRedirect("/runs/new", "runs new anonymous", "runs-new-anonymous", "/login?redirect=%2Fruns%2Fnew", Heading("Sign In", "Kirjaudu")),
Authenticated("/runs/new", "runs new authenticated", "runs-new-authenticated", Heading("Schedule a run", "Uusi runi"), SelectDungeonAsync),

ProtectedRedirect($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit anonymous", "runs-edit-anonymous", "/login?redirect=%2Fruns%2Fe2e-run-001%2Fedit", Heading("Sign In", "Kirjaudu")),
Authenticated($"/runs/{DefaultSeed.TestRunId}/edit", "runs edit authenticated", "runs-edit-authenticated", Heading("Edit Run", "Muokkaa runia")),

ProtectedRedirect("/characters", "characters anonymous", "characters-anonymous", "/login?redirect=%2Fcharacters", Heading("Sign In", "Kirjaudu")),
Authenticated("/characters", "characters authenticated", "characters-authenticated", Heading("My Characters", "Hahmoni")),

ProtectedRedirect("/guild", "guild anonymous", "guild-anonymous", "/login?redirect=%2Fguild", Heading("Sign In", "Kirjaudu")),
Authenticated("/guild", "guild authenticated", "guild-authenticated", Heading("Guild", "Kilta")),

ProtectedRedirect("/guild/admin", "guild admin anonymous", "guild-admin-anonymous", "/login?redirect=%2Fguild%2Fadmin", Heading("Sign In", "Kirjaudu")),
SiteAdmin("/guild/admin", "guild admin site admin", "guild-admin-site-admin", Heading("Guild Admin", "Killan hallinta"), LoadGuildAdminAsync),

ProtectedRedirect("/admin/reference", "admin reference anonymous", "admin-reference-anonymous", "/login?redirect=%2Fadmin%2Freference", Heading("Sign In", "Kirjaudu")),
SiteAdmin("/admin/reference", "admin reference site admin", "admin-reference-site-admin", Heading("Reference data", "Referenssitiedot")),

ProtectedRedirect("/instances", "instances anonymous", "instances-anonymous", "/login?redirect=%2Finstances", Heading("Sign In", "Kirjaudu")),
Authenticated("/instances", "instances authenticated", "instances-authenticated", Heading("Instances", "Instanssit")),
```

Update the private factory methods to accept `Func<IPage, Task> waitForReadyAsync` and optional `Func<IPage, string, string, Task>? prepareAsync`, then pass them into `VisualRouteState`.

- [ ] **Step 3: Run the model tests after manifest update**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter FullyQualifiedName~VisualRouteArtifactModelSpec --no-restore
```

Expected: PASS. If a localized string mismatch breaks compilation or test expectations, inspect `app/wwwroot/locales/en.json` and `app/wwwroot/locales/fi.json`, then update the manifest to match the shipped text.

- [ ] **Step 4: Add the browser matrix spec**

Create `tests/Lfm.E2E/Specs/VisualRouteArtifactsSpec.cs`:

```csharp
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
        var context = await CreateContextAsync(item);
        var page = await context.NewPageAsync();
        AttachDiagnostics(page, diagnostics);

        try
        {
            await StubPortraitsAsync(page);
            await AuthenticateIfNeededAsync(page, item.State);

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
            return ToEntry(
                item,
                page.Url,
                VisualRouteArtifactPaths.ScreenshotRelativePath(item.Variant, item.Viewport, item.State),
                "failed",
                ex.Message);
        }
        finally
        {
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
            state.Path);
    }

    private static void AttachDiagnostics(IPage page, VisualDiagnostics diagnostics)
    {
        page.Console += (_, msg) =>
        {
            if (msg.Type is "error" or "warning")
                diagnostics.Messages.Enqueue($"[Browser {msg.Type.ToUpper()}] {msg.Text}");
        };

        page.RequestFailed += (_, req) =>
            diagnostics.Messages.Enqueue($"[Browser REQUESTFAILED] {req.Url} - {req.Failure}");
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
                    !message.Contains("/api/v1/me", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (messages.Length == 0)
                return;

            throw new XunitException(
                $"Unexpected browser diagnostics for {item.State.Name} [{item.Viewport.Name}, {item.Variant.Name}]:\n" +
                string.Join("\n", messages));
        }
    }
}
```

- [ ] **Step 5: Run the visual artifacts lane**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter Category=VisualArtifacts --no-restore
```

Expected: PASS after the implementation is stable. If it fails, inspect:

- xUnit failure message for the failing route/state/variant/viewport.
- `artifacts/e2e-results/visual-routes/index.json`
- any partial screenshots under `artifacts/e2e-results/visual-routes/`
- `artifacts/e2e-results/api.log` for backend startup or API failures.

- [ ] **Step 6: Verify artifact count and index content**

Run:

```bash
jq '.count, (.entries | length), ([.entries[].status] | unique)' artifacts/e2e-results/visual-routes/index.json
```

Expected output:

```text
300
300
[
  "captured"
]
```

- [ ] **Step 7: Commit Task 4**

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E/Helpers/VisualRouteArtifactModel.cs tests/Lfm.E2E/Helpers/VisualRouteManifest.cs tests/Lfm.E2E/Specs/VisualRouteArtifactModelSpec.cs tests/Lfm.E2E/Specs/VisualRouteArtifactsSpec.cs
git -C /home/souroldgeezer/repos/lfm commit -m "Capture visual route artifacts"
```

---

### Task 5: Drift Check And Full E2E Verification

**Files:**
- No planned file changes.

- [ ] **Step 1: Run the E2E drift preflight**

Run:

```bash
bash ./scripts/check-e2e-drift.sh
```

Expected: PASS. If it flags a route or selector that changed because of this work, update the manifest or the drift script only when the script is identifying real stale E2E surface.

- [ ] **Step 2: Run the visual lane again after drift check**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter Category=VisualArtifacts --no-restore
```

Expected: PASS with `300` captured entries.

- [ ] **Step 3: Run the full E2E suite**

Run:

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release
```

Expected: PASS. Only claim "full E2E passed" after this command succeeds.

- [ ] **Step 4: Inspect representative screenshots**

Open these artifacts from the same run and confirm they are nonblank and show the intended state:

```text
artifacts/e2e-results/visual-routes/default/desktop/landing.png
artifacts/e2e-results/visual-routes/default/phone/runs-authenticated.png
artifacts/e2e-results/visual-routes/fi/mobile-floor/runs-new-authenticated.png
artifacts/e2e-results/visual-routes/dark/desktop/admin-reference-site-admin.png
artifacts/e2e-results/visual-routes/forced-colors/mobile-floor/guild-admin-site-admin.png
```

Expected: each screenshot shows the named route, not a blank loading page or wrong redirect state. Record any visual defect as a product issue or fix it in the owning page when the defect is caused by this change's route setup.

- [ ] **Step 5: Commit any verification-driven fix**

If Task 5 required code edits, commit them:

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E
git -C /home/souroldgeezer/repos/lfm commit -m "Stabilize visual artifacts"
```

If Task 5 required no edits, do not create an empty commit.

---

### Task 6: Format, Build, And Final Branch State

**Files:**
- No planned file changes unless formatting changes C# files.

- [ ] **Step 1: Run C# formatting**

Run:

```bash
dotnet format lfm.sln
```

Expected: completes successfully. If it changes files, inspect and commit the formatting changes with the relevant task files.

- [ ] **Step 2: Run the format gate**

Run:

```bash
dotnet format lfm.sln --verify-no-changes --no-restore --severity error
```

Expected: PASS.

- [ ] **Step 3: Run the Release build**

Run:

```bash
dotnet build lfm.sln -c Release
```

Expected: PASS.

- [ ] **Step 4: Run vulnerable package audit**

Run:

```bash
dotnet list lfm.sln package --vulnerable --include-transitive
```

Expected: completes with no vulnerable packages reported. This work should not add packages; any existing advisory is outside this plan and must be reported.

- [ ] **Step 5: Review the final diff**

Run:

```bash
git -C /home/souroldgeezer/repos/lfm diff --stat main..HEAD
git -C /home/souroldgeezer/repos/lfm diff --check main..HEAD
```

Expected: diff stays inside `tests/Lfm.E2E` plus this plan/spec if they remain on the implementation branch; whitespace check passes.

- [ ] **Step 6: Final commit if needed**

If formatting or verification fixes changed files after the last task commit:

```bash
git -C /home/souroldgeezer/repos/lfm add tests/Lfm.E2E
git -C /home/souroldgeezer/repos/lfm commit -m "Verify visual artifacts"
```

If the branch is already clean, do not create an empty commit.

---

## Implementation Notes

- Keep screenshots out of git. `artifacts/` is already ignored and uploaded by the E2E workflow.
- Keep visual artifacts out of the smoke lane. Use `Category=VisualArtifacts` for targeted execution.
- The new lane is intentionally artifact-heavy. Avoid adding workflow-specific upload steps unless the existing `artifacts/e2e-results` upload stops including the new folder.
- `ForcedColors.Active`, `ColorScheme.Dark`, `Locale = "fi-FI"`, and `ViewportSize` are supported by Microsoft.Playwright 1.59.0 `BrowserNewContextOptions`.
- `VisualRouteArtifactsSpec` should not inherit `E2ETestBase` unless it sets `Page` and `Context` for every matrix entry. The plan uses a self-contained diagnostics helper because the spec owns many pages inside one xUnit test.

## Self-Review Checklist

- Spec coverage: every approved route, state, viewport, variant, local artifact, semantic health check, and no-baseline requirement maps to Tasks 1-4.
- Verification coverage: drift preflight, targeted lane, full E2E, formatting, build, package audit, and artifact inspection map to Tasks 5-6.
- File boundaries: metadata, fixture, model, manifest, writer, model tests, and browser spec each have one clear responsibility.
