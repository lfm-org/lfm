# LFM Architecture

The canonical architecture source is the single consolidated ArchiMate dediren
package [`lfm.dediren/`](lfm.dediren/). It was extracted from the current .NET
solution, Azure Bicep infrastructure, GitHub Actions deploy workflows, and Blazor
route/API surface. Every layer — Application, Technology, Business process
candidates, and Implementation & Migration — plus a portfolio `system-landscape`
front-page lives in this one package; behavior is expressed with ArchiMate
process, function, and cooperation views rather than separate UML packages.

The package is intentionally source-first:

- Application, Technology, and Implementation & Migration elements are lifted
  from repository source files.
- The run-signup Business Process view is a source-evidenced candidate derived
  from the UI and API flow. Actor naming, process boundaries, and terminal event
  wording still need architect/stakeholder confirmation.
- Capability Map, Motivation, Strategy, and Physical views are not generated
  here because they are architect-owned, not source-derived.
- OEF export is not configured yet. SVG render output is the primary review
  proof for dediren packages.

## Package Files

Package directories use stable source-package names, not view names:

```text
docs/architecture/<system>.dediren/
docs/architecture/<system>-<scope>-<profile>.dediren/
```

Use the short system name for the canonical system-level package,
`lfm.dediren`. The repository currently ships this one consolidated package. The
`<system>-<scope>-<profile>.dediren` form is reserved for any future supplemental
package: `<scope>` identifies a durable domain, workflow, or capability slice, and
`<profile>` identifies the notation/profile when the package is profile-specific.
Keep per-view detail in `project.json` and render filenames instead of creating one
package directory per diagram.

| File | Purpose |
|---|---|
| [`lfm.dediren/project.json`](lfm.dediren/project.json) | Project manifest and actual view list (title, architecture question, diagram kind, render targets per view) |
| [`lfm.dediren/model.json`](lfm.dediren/model.json) | Canonical source graph, relationships, source evidence, and view membership |
| [`lfm.dediren/render-policy.json`](lfm.dediren/render-policy.json) | ArchiMate SVG page, layer fills, per-type decorators, and relationship markers |
| [`lfm.dediren/gallery.html`](lfm.dediren/gallery.html) | Self-contained, shareable browser gallery of all rendered views — open straight from disk, no network or external assets |

Per-view render metadata, layout output, and intermediate SVGs are generated
from the packages. `*.dediren/generated/` is reproducible tool output and
ignored by git. Reviewable render snapshots live under
`docs/architecture/renders/<package-name>/`, where `<package-name>` matches the
source package directory without the `.dediren` suffix.

`gallery.html` is a committed viewer over those render snapshots: it inlines
every view's SVG (byte-identical to the matching `renders/<package-name>/`
snapshot) with a register, per-view node/relationship counts, deep-linkable
views, and light/dark theming. It is regenerated from the package's own
`project.json` plus the rendered SVGs and render metadata by the
`souroldgeezer-architecture` skill's `build-gallery.py`, so a re-render or a
new view means rebuilding it (`build-gallery.py --check` gates drift).

## Views

| # | View | Diagram kind | Render | Source scope |
|---|---|---|---|---|
| 1 | LFM - System Landscape | Landscape | [`system-landscape.svg`](renders/lfm/system-landscape.svg) | portfolio front-page: `Lfm.App`, `Lfm.Api`, `Lfm.Contracts`, the REST API surface, the Azure hosting platform (Static Web Apps, Function App, Cosmos, Storage, Key Vault, Application Insights), and the Battle.net/Blizzard external services |
| 2 | LFM - Technology Usage Overview | Technology Usage | [`technology-usage.svg`](renders/lfm/technology-usage.svg) | top-level Azure hosting, runtime identity, platform dependencies, telemetry |
| 3 | LFM - Data Plane and Secrets | Technology Usage | [`technology-data-plane.svg`](renders/lfm/technology-data-plane.svg) | Cosmos containers, blob reference/media-cache paths, Key Vault, data-protection key, Function App managed identity |
| 4 | LFM - Observability and Alerting | Technology Usage | [`technology-observability.svg`](renders/lfm/technology-observability.svg) | Application Insights, Log Analytics, Cosmos throttle alert, telemetry publishing role |
| 5 | LFM - Production Release Migration | Migration | [`implementation-migration-production-release-migration.svg`](renders/lfm/implementation-migration-production-release-migration.svg) | local hook gates, `.github/workflows/deploy*.yml`, `secrets-scan.yml`, `analyze-infra.yml`, `infra/**/*.bicep`, deployable app/API projects |
| 6 | Run Signup - Business Process Candidate | Business Process Cooperation | [`business-run-signup-process-candidate.svg`](renders/lfm/business-run-signup-process-candidate.svg) | `/runs` UI flow and `Runs*Function` signup endpoints; source-derived candidate |
| 7 | Run Signup - Service Realization | Service Realization | [`application-run-signup-service-realization.svg`](renders/lfm/application-run-signup-service-realization.svg) | run-signup process candidate, `Run Management` service, `Lfm.Api`, Cosmos containers |
| 8 | Run Maintenance - Application Process | Application Process | [`application-run-maintenance-application-process.svg`](renders/lfm/application-run-maintenance-application-process.svg) | `CreateRunPage`, `EditRunPage`, `RunsCreateFunction`, `RunsUpdateFunction`, `RunsDeleteFunction`, run Cosmos state |
| 9 | Auth and Profile - Service Realization | Service Realization | [`application-auth-profile-realization.svg`](renders/lfm/application-auth-profile-realization.svg) | sign-in UI, expired-session detection, `BattleNet*Function`, `Me*Function`, `Raider*Function`, `WowMediaCacheFunction`, raider Cosmos state, media-cache blobs, Battle.net OAuth, WoW Profile API, render CDN source |
| 10 | Account Deletion - Application Process | Application Process | [`application-account-deletion-application-process.svg`](renders/lfm/application-account-deletion-application-process.svg) | `CharactersPage`, `MeClient.DeleteAsync`, `MeDeleteFunction`, raider/run/idempotency data effects |
| 11 | Guild - Service Realization | Service Realization | [`application-guild-realization.svg`](renders/lfm/application-guild-realization.svg) | `/guild` and `/guild/admin` UI, `GuildFunction`, `GuildAdminFunction`, guild/raider Cosmos state, WoW Profile API guild refresh |
| 12 | WoW Reference Data - Service Realization | Service Realization | [`application-wow-reference-realization.svg`](renders/lfm/application-wow-reference-realization.svg) | reference-data UI consumers, admin refresh UI, `WowReference*` read endpoints, `WowReferenceRefresh*` endpoints, `WowMediaCacheFunction`, blob reference/media-cache data, WoW Game Data API, render CDN source |
| 13 | Operational Readiness - Service Realization | Service Realization | [`application-ops-health-realization.svg`](renders/lfm/application-ops-health-realization.svg) | `HealthFunction`, `RunsMigrateSchemaFunction`, Cosmos readiness, Application Insights |
| 14 | API Request Pipeline - Application Cooperation | Application Cooperation | [`application-request-pipeline.svg`](renders/lfm/application-request-pipeline.svg) | ordered `api/Middleware/*` request stages (CORS → security headers → request size → rate limit → audit → auth → auth policy → idempotency) wired in `api/Program.cs`, plus the outbound `BlizzardRateLimiter`/`BlizzardRateLimitHandler` gateway over the Blizzard services |

## Source Provenance

| ArchiMate layer | Lifted from |
|---|---|
| Application | [`lfm.sln`](../../lfm.sln), [`api/Lfm.Api.csproj`](../../api/Lfm.Api.csproj), [`app/Lfm.App.csproj`](../../app/Lfm.App.csproj), [`app/Lfm.App.Core/Lfm.App.Core.csproj`](../../app/Lfm.App.Core/Lfm.App.Core.csproj), [`shared/Lfm.Contracts/Lfm.Contracts.csproj`](../../shared/Lfm.Contracts/Lfm.Contracts.csproj), [`api/Program.cs`](../../api/Program.cs), [`api/Functions/`](../../api/Functions/), [`api/Middleware/`](../../api/Middleware/), [`app/Lfm.App.Core/Services/`](../../app/Lfm.App.Core/Services/), [`app/Pages/`](../../app/Pages/) |
| Technology | [`infra/main.bicep`](../../infra/main.bicep), [`infra/modules/`](../../infra/modules/), [`api/Program.cs`](../../api/Program.cs), [`api/host.json`](../../api/host.json) |
| Implementation & Migration | [`scripts/pre-commit`](../../scripts/pre-commit), [`scripts/pre-push`](../../scripts/pre-push), [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml), [`.github/workflows/secrets-scan.yml`](../../.github/workflows/secrets-scan.yml), [`.github/workflows/analyze-infra.yml`](../../.github/workflows/analyze-infra.yml), [`.github/workflows/deploy-infra.yml`](../../.github/workflows/deploy-infra.yml), [`.github/workflows/deploy-app-build.yml`](../../.github/workflows/deploy-app-build.yml), [`.github/workflows/deploy-app.yml`](../../.github/workflows/deploy-app.yml) |
| Business Process candidates | [`app/Pages/RunsPage.razor`](../../app/Pages/RunsPage.razor), [`app/Lfm.App.Core/Services/RunsClient.cs`](../../app/Lfm.App.Core/Services/RunsClient.cs), [`api/Functions/RunsListFunction.cs`](../../api/Functions/RunsListFunction.cs), [`api/Functions/RunsDetailFunction.cs`](../../api/Functions/RunsDetailFunction.cs), [`api/Functions/RunsSignupFunction.cs`](../../api/Functions/RunsSignupFunction.cs), [`api/Functions/RunsSignupOptionsFunction.cs`](../../api/Functions/RunsSignupOptionsFunction.cs), [`api/Functions/RunsCancelSignupFunction.cs`](../../api/Functions/RunsCancelSignupFunction.cs) |
| Application Process views | [`app/Pages/CreateRunPage.razor`](../../app/Pages/CreateRunPage.razor), [`app/Pages/EditRunPage.razor`](../../app/Pages/EditRunPage.razor), [`app/Pages/CharactersPage.razor`](../../app/Pages/CharactersPage.razor), [`app/Lfm.App.Core/Services/MeClient.cs`](../../app/Lfm.App.Core/Services/MeClient.cs), [`api/Functions/RunsCreateFunction.cs`](../../api/Functions/RunsCreateFunction.cs), [`api/Functions/RunsUpdateFunction.cs`](../../api/Functions/RunsUpdateFunction.cs), [`api/Functions/RunsDeleteFunction.cs`](../../api/Functions/RunsDeleteFunction.cs), [`api/Functions/MeDeleteFunction.cs`](../../api/Functions/MeDeleteFunction.cs) |
| Service realization | [`app/Pages/LoginPage.razor`](../../app/Pages/LoginPage.razor), [`app/Pages/CharactersPage.razor`](../../app/Pages/CharactersPage.razor), [`app/Pages/GoodbyePage.razor`](../../app/Pages/GoodbyePage.razor), [`app/Pages/GuildPage.razor`](../../app/Pages/GuildPage.razor), [`app/Pages/GuildAdminPage.razor`](../../app/Pages/GuildAdminPage.razor), [`app/Pages/InstancesPage.razor`](../../app/Pages/InstancesPage.razor), [`app/Pages/CreateRunPage.razor`](../../app/Pages/CreateRunPage.razor), [`app/Pages/EditRunPage.razor`](../../app/Pages/EditRunPage.razor), [`app/Pages/AdminReferenceRefreshPage.razor`](../../app/Pages/AdminReferenceRefreshPage.razor), [`app/Pages/PrivacyPolicyPage.razor`](../../app/Pages/PrivacyPolicyPage.razor), [`api/Functions/`](../../api/Functions/), [`api/Services/`](../../api/Services/), [`api/Repositories/`](../../api/Repositories/) |

## Rendering And Validation

Use the bundled `dediren` runtime from the `souroldgeezer-architecture`
plugin. The canonical evidence loop is:

```bash
dediren validate --input docs/architecture/lfm.dediren/model.json
dediren validate --plugin generic-graph --profile archimate --input docs/architecture/lfm.dediren/model.json
dediren project --input docs/architecture/lfm.dediren/model.json --plugin generic-graph --view technology-usage --target layout-request
dediren project --input docs/architecture/lfm.dediren/model.json --plugin generic-graph --view technology-usage --target render-metadata
dediren layout --plugin elk-layout --input <projection.json>
dediren validate-layout --input <layout.json>
dediren render --plugin render --policy docs/architecture/lfm.dediren/render-policy.json --metadata <render-metadata.json> --input <layout.json> > <view>.render.json
jq -r '.data.artifacts[] | select(.artifact_kind=="svg") | .content' <view>.render.json > <view>.svg
```

Repeat projection/layout/render for every view listed in the package manifest.
`dediren render` emits a JSON result envelope; the SVG payload is the
`.data.artifacts[]` entry whose `artifact_kind` is `svg`, extracted to the `.svg`
path declared by the project manifest. Each rendered SVG then gets the
architecture-design skill's `svg-accessible-name.sh` post-render step (a
`role="img"` accessible name and a visible per-view title band). Copy reviewable
SVG snapshots to [`renders/lfm/`](renders/lfm/) and keep the README view
inventory, render links, and source provenance aligned with the package.
