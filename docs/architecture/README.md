# LFM Architecture

The canonical architecture source is now the ArchiMate dediren package
[`lfm.dediren/`](lfm.dediren/). It was extracted from the current .NET solution,
Azure Bicep infrastructure, GitHub Actions deploy workflows, and Blazor route/API
surface. UML handoff detail lives in separate UML dediren packages when a flow
needs implementation-level structure beyond the ArchiMate views.

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

Use the short system name for the canonical system-level package, such as
`lfm.dediren`. Use `<system>-<scope>-<profile>.dediren` for supplemental
implementation handoff packages, such as `lfm-run-signup-uml.dediren`. The
`<scope>` segment should identify a durable domain, workflow, or capability
slice; the `<profile>` segment should identify the notation/profile when the
package is profile-specific. Keep per-view detail in `project.json` and render
filenames instead of creating one package directory per diagram.

| File | Purpose |
|---|---|
| [`lfm.dediren/project.json`](lfm.dediren/project.json) | Project manifest and actual view list |
| [`lfm.dediren/model.json`](lfm.dediren/model.json) | Canonical source graph, relationships, source evidence, and view membership |
| [`lfm.dediren/render-policy.json`](lfm.dediren/render-policy.json) | SVG page, colors, decorators, and relationship markers |
| [`lfm-run-signup-uml.dediren/project.json`](lfm-run-signup-uml.dediren/project.json) | UML project manifest for the run-signup implementation handoff |
| [`lfm-run-signup-uml.dediren/model.json`](lfm-run-signup-uml.dediren/model.json) | UML activity, data, and class-boundary source graph linked to ArchiMate context |
| [`lfm-run-signup-uml.dediren/render-policy.json`](lfm-run-signup-uml.dediren/render-policy.json) | Black-and-white UML SVG render policy |
| [`lfm-application-handoff-uml.dediren/project.json`](lfm-application-handoff-uml.dediren/project.json) | UML project manifest for remaining application implementation handoffs |
| [`lfm-application-handoff-uml.dediren/model.json`](lfm-application-handoff-uml.dediren/model.json) | UML activity, data, and class-boundary source graph for application handoffs linked to ArchiMate context |
| [`lfm-application-handoff-uml.dediren/render-policy.json`](lfm-application-handoff-uml.dediren/render-policy.json) | Black-and-white UML SVG render policy |
| [`lfm-platform-operations-uml.dediren/project.json`](lfm-platform-operations-uml.dediren/project.json) | UML project manifest for platform and operations handoffs |
| [`lfm-platform-operations-uml.dediren/model.json`](lfm-platform-operations-uml.dediren/model.json) | UML class-boundary, data-plane, observability, and release source graph linked to ArchiMate context |
| [`lfm-platform-operations-uml.dediren/render-policy.json`](lfm-platform-operations-uml.dediren/render-policy.json) | Black-and-white UML SVG render policy |

Per-view render metadata, layout output, and intermediate SVGs are generated
from the packages. `*.dediren/generated/` is reproducible tool output and
ignored by git. Reviewable render snapshots live under
`docs/architecture/renders/<package-name>/`, where `<package-name>` matches the
source package directory without the `.dediren` suffix.

## Views

| # | View | Diagram kind | Render | Source scope |
|---|---|---|---|---|
| 1 | LFM - Technology Usage Overview | Technology Usage | [`technology-usage.svg`](renders/lfm/technology-usage.svg) | top-level Azure hosting, runtime identity, platform dependencies, telemetry |
| 2 | LFM - Data Plane and Secrets | Technology Usage | [`technology-data-plane.svg`](renders/lfm/technology-data-plane.svg) | Cosmos containers, blob containers, Key Vault, data-protection key, Function App managed identity |
| 3 | LFM - Observability and Alerting | Technology Usage | [`technology-observability.svg`](renders/lfm/technology-observability.svg) | Application Insights, Log Analytics, Cosmos throttle alert, telemetry publishing role |
| 4 | LFM - Production Release Migration | Migration | [`implementation-migration-production-release-migration.svg`](renders/lfm/implementation-migration-production-release-migration.svg) | local hook gates, `.github/workflows/deploy*.yml`, `secrets-scan.yml`, `analyze-infra.yml`, `infra/**/*.bicep`, deployable app/API projects |
| 5 | Run Signup - Business Process Candidate | Business Process Cooperation | [`business-run-signup-process-candidate.svg`](renders/lfm/business-run-signup-process-candidate.svg) | `/runs` UI flow and `Runs*Function` signup endpoints; source-derived candidate |
| 6 | Run Signup - Service Realization | Service Realization | [`application-run-signup-service-realization.svg`](renders/lfm/application-run-signup-service-realization.svg) | run-signup process candidate, `Run Management` service, `Lfm.Api`, Cosmos containers |
| 7 | Run Maintenance - Application Process | Application Process | [`application-run-maintenance-application-process.svg`](renders/lfm/application-run-maintenance-application-process.svg) | `CreateRunPage`, `EditRunPage`, `RunsCreateFunction`, `RunsUpdateFunction`, `RunsDeleteFunction`, run Cosmos state |
| 8 | Auth and Profile - Service Realization | Service Realization | [`application-auth-profile-realization.svg`](renders/lfm/application-auth-profile-realization.svg) | sign-in UI, `BattleNet*Function`, `Me*Function`, `Raider*Function`, raider Cosmos state, Battle.net OAuth, WoW Profile API, render CDN |
| 9 | Account Deletion - Application Process | Application Process | [`application-account-deletion-application-process.svg`](renders/lfm/application-account-deletion-application-process.svg) | `CharactersPage`, `MeClient.DeleteAsync`, `MeDeleteFunction`, raider/run/idempotency data effects |
| 10 | Guild - Service Realization | Service Realization | [`application-guild-realization.svg`](renders/lfm/application-guild-realization.svg) | `/guild` and `/guild/admin` UI, `GuildFunction`, `GuildAdminFunction`, guild/raider Cosmos state, WoW Profile API guild refresh |
| 11 | WoW Reference Data - Service Realization | Service Realization | [`application-wow-reference-realization.svg`](renders/lfm/application-wow-reference-realization.svg) | reference-data UI consumers, admin refresh UI, `WowReference*` read endpoints, `WowReferenceRefresh*` endpoints, blob reference data, WoW Game Data API |
| 12 | Operational Readiness - Service Realization | Service Realization | [`application-ops-health-realization.svg`](renders/lfm/application-ops-health-realization.svg) | `HealthFunction`, `RunsMigrateSchemaFunction`, Cosmos readiness, Application Insights |

## UML Handoff Views

| # | View | Diagram kind | Render | Source scope |
|---|---|---|---|---|
| 1 | Run Signup - UML Activity | UML Activity | [`uml-run-signup-activity.svg`](renders/lfm-run-signup-uml/uml-run-signup-activity.svg) | `RunsPage`, `RunsClient`, signup-options, signup, and cancel-signup HTTP paths |
| 2 | Run Signup - UML Data | UML Data | [`uml-run-signup-data.svg`](renders/lfm-run-signup-uml/uml-run-signup-data.svg) | signup request/options/detail DTOs plus `RunDocument`, `RunCharacterEntry`, and raider character state |
| 3 | Run Signup - UML Class Boundary | UML Class | [`uml-run-signup-class.svg`](renders/lfm-run-signup-uml/uml-run-signup-class.svg) | Blazor client boundary, Function adapters, run-signup services, policy gates, and repositories |
| 4 | Run Maintenance - UML Activity | UML Activity | [`run-maintenance-activity.svg`](renders/lfm-application-handoff-uml/run-maintenance-activity.svg) | create, edit, and delete run UI/API paths plus run repository effects |
| 5 | Auth and Profile - UML Activity | UML Activity | [`auth-profile-activity.svg`](renders/lfm-application-handoff-uml/auth-profile-activity.svg) | Battle.net sign-in, identity probe, profile refresh, and raider profile persistence paths |
| 6 | Account Deletion - UML Activity | UML Activity | [`account-deletion-activity.svg`](renders/lfm-application-handoff-uml/account-deletion-activity.svg) | account deletion request, goodbye redirect, raider cleanup, run cleanup, and idempotency effects |
| 7 | Guild - UML Activity | UML Activity | [`guild-activity.svg`](renders/lfm-application-handoff-uml/guild-activity.svg) | guild read and admin refresh flows across UI, API, repository, and WoW Profile API |
| 8 | WoW Reference Data - UML Activity | UML Activity | [`wow-reference-activity.svg`](renders/lfm-application-handoff-uml/wow-reference-activity.svg) | reference-data reads, admin refresh, blob reference storage, and WoW Game Data API |
| 9 | Operational Readiness - UML Activity | UML Activity | [`ops-readiness-activity.svg`](renders/lfm-application-handoff-uml/ops-readiness-activity.svg) | health probe, schema migration, Cosmos readiness, and telemetry publication paths |
| 10 | Application Handoff - UML Class Boundary | UML Class | [`application-handoff-class.svg`](renders/lfm-application-handoff-uml/application-handoff-class.svg) | UI pages, app-core clients, API functions, service layer, repositories, and external ports |
| 11 | Application Handoff - UML Data | UML Data | [`application-handoff-data.svg`](renders/lfm-application-handoff-uml/application-handoff-data.svg) | LFM contracts, Cosmos document families, idempotency state, blob reference data, and external API data |
| 12 | Platform Hosting - UML Class Boundary | UML Class | [`platform-hosting-class.svg`](renders/lfm-platform-operations-uml/platform-hosting-class.svg) | Static Web Apps, Function App, runtime, API, identity, data/secrets/telemetry ports |
| 13 | Platform Data Plane - UML Data | UML Data | [`platform-data-plane.svg`](renders/lfm-platform-operations-uml/platform-data-plane.svg) | Cosmos database/containers, storage blob containers, Key Vault key, data-protection key ring, managed identity access |
| 14 | Observability - UML Activity | UML Activity | [`observability-activity.svg`](renders/lfm-platform-operations-uml/observability-activity.svg) | API telemetry, Application Insights ingestion, Log Analytics aggregation, Cosmos throttle alert path |
| 15 | Production Release - UML Activity | UML Activity | [`production-release-activity.svg`](renders/lfm-platform-operations-uml/production-release-activity.svg) | local hook gates, deploy workflow, change detection, secret scan, infra analysis/deploy, app build/deploy |

The UML packages link upward to ArchiMate element ids through
`properties.uml.architecture_context`. UML remains handoff elaboration:
ArchiMate stays the canonical architecture topology, while the UML packages show
implementation handoff flows, boundaries, data shapes, and platform-operation
sequences.

## Source Provenance

| ArchiMate layer | Lifted from |
|---|---|
| Application | [`lfm.sln`](../../lfm.sln), [`api/Lfm.Api.csproj`](../../api/Lfm.Api.csproj), [`app/Lfm.App.csproj`](../../app/Lfm.App.csproj), [`app/Lfm.App.Core/Lfm.App.Core.csproj`](../../app/Lfm.App.Core/Lfm.App.Core.csproj), [`shared/Lfm.Contracts/Lfm.Contracts.csproj`](../../shared/Lfm.Contracts/Lfm.Contracts.csproj), [`api/Program.cs`](../../api/Program.cs), [`api/Functions/`](../../api/Functions/), [`app/Lfm.App.Core/Services/`](../../app/Lfm.App.Core/Services/), [`app/Pages/`](../../app/Pages/) |
| Technology | [`infra/main.bicep`](../../infra/main.bicep), [`infra/modules/`](../../infra/modules/), [`api/Program.cs`](../../api/Program.cs), [`api/host.json`](../../api/host.json) |
| Implementation & Migration | [`scripts/pre-commit`](../../scripts/pre-commit), [`scripts/pre-push`](../../scripts/pre-push), [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml), [`.github/workflows/secrets-scan.yml`](../../.github/workflows/secrets-scan.yml), [`.github/workflows/analyze-infra.yml`](../../.github/workflows/analyze-infra.yml), [`.github/workflows/deploy-infra.yml`](../../.github/workflows/deploy-infra.yml), [`.github/workflows/deploy-app-build.yml`](../../.github/workflows/deploy-app-build.yml), [`.github/workflows/deploy-app.yml`](../../.github/workflows/deploy-app.yml) |
| Business Process candidates | [`app/Pages/RunsPage.razor`](../../app/Pages/RunsPage.razor), [`app/Lfm.App.Core/Services/RunsClient.cs`](../../app/Lfm.App.Core/Services/RunsClient.cs), [`api/Functions/RunsListFunction.cs`](../../api/Functions/RunsListFunction.cs), [`api/Functions/RunsDetailFunction.cs`](../../api/Functions/RunsDetailFunction.cs), [`api/Functions/RunsSignupFunction.cs`](../../api/Functions/RunsSignupFunction.cs), [`api/Functions/RunsSignupOptionsFunction.cs`](../../api/Functions/RunsSignupOptionsFunction.cs), [`api/Functions/RunsCancelSignupFunction.cs`](../../api/Functions/RunsCancelSignupFunction.cs) |
| Application Process views | [`app/Pages/CreateRunPage.razor`](../../app/Pages/CreateRunPage.razor), [`app/Pages/EditRunPage.razor`](../../app/Pages/EditRunPage.razor), [`app/Pages/CharactersPage.razor`](../../app/Pages/CharactersPage.razor), [`app/Lfm.App.Core/Services/MeClient.cs`](../../app/Lfm.App.Core/Services/MeClient.cs), [`api/Functions/RunsCreateFunction.cs`](../../api/Functions/RunsCreateFunction.cs), [`api/Functions/RunsUpdateFunction.cs`](../../api/Functions/RunsUpdateFunction.cs), [`api/Functions/RunsDeleteFunction.cs`](../../api/Functions/RunsDeleteFunction.cs), [`api/Functions/MeDeleteFunction.cs`](../../api/Functions/MeDeleteFunction.cs) |
| Service realization | [`app/Pages/LoginPage.razor`](../../app/Pages/LoginPage.razor), [`app/Pages/CharactersPage.razor`](../../app/Pages/CharactersPage.razor), [`app/Pages/GoodbyePage.razor`](../../app/Pages/GoodbyePage.razor), [`app/Pages/GuildPage.razor`](../../app/Pages/GuildPage.razor), [`app/Pages/GuildAdminPage.razor`](../../app/Pages/GuildAdminPage.razor), [`app/Pages/InstancesPage.razor`](../../app/Pages/InstancesPage.razor), [`app/Pages/CreateRunPage.razor`](../../app/Pages/CreateRunPage.razor), [`app/Pages/EditRunPage.razor`](../../app/Pages/EditRunPage.razor), [`app/Pages/AdminReferenceRefreshPage.razor`](../../app/Pages/AdminReferenceRefreshPage.razor), [`app/Pages/PrivacyPolicyPage.razor`](../../app/Pages/PrivacyPolicyPage.razor), [`api/Functions/`](../../api/Functions/), [`api/Services/`](../../api/Services/), [`api/Repositories/`](../../api/Repositories/) |
| Supplemental UML handoff | [`docs/architecture/lfm.dediren/model.json`](lfm.dediren/model.json), [`app/Pages/`](../../app/Pages/), [`app/Lfm.App.Core/Services/`](../../app/Lfm.App.Core/Services/), [`shared/Lfm.Contracts/`](../../shared/Lfm.Contracts/), [`api/Functions/`](../../api/Functions/), [`api/Services/`](../../api/Services/), [`api/Repositories/`](../../api/Repositories/) |
| Platform and operations UML handoff | [`docs/architecture/lfm.dediren/model.json`](lfm.dediren/model.json), [`infra/modules/`](../../infra/modules/), [`infra/main.bicep`](../../infra/main.bicep), [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml), [`.github/workflows/deploy-infra.yml`](../../.github/workflows/deploy-infra.yml), [`.github/workflows/deploy-app-build.yml`](../../.github/workflows/deploy-app-build.yml), [`.github/workflows/deploy-app.yml`](../../.github/workflows/deploy-app.yml), [`.github/workflows/secrets-scan.yml`](../../.github/workflows/secrets-scan.yml), [`.github/workflows/analyze-infra.yml`](../../.github/workflows/analyze-infra.yml), [`scripts/pre-commit`](../../scripts/pre-commit), [`scripts/pre-push`](../../scripts/pre-push), [`api/Program.cs`](../../api/Program.cs) |

## Rendering And Validation

Use the bundled `dediren` runtime from the `souroldgeezer-architecture`
plugin. The canonical evidence loop is:

```bash
dediren validate --input docs/architecture/lfm.dediren/model.json
dediren validate --plugin generic-graph --profile archimate --input docs/architecture/lfm.dediren/model.json
dediren validate --input docs/architecture/lfm-run-signup-uml.dediren/model.json
dediren validate --plugin generic-graph --profile uml --input docs/architecture/lfm-run-signup-uml.dediren/model.json
dediren validate --input docs/architecture/lfm-application-handoff-uml.dediren/model.json
dediren validate --plugin generic-graph --profile uml --input docs/architecture/lfm-application-handoff-uml.dediren/model.json
dediren validate --input docs/architecture/lfm-platform-operations-uml.dediren/model.json
dediren validate --plugin generic-graph --profile uml --input docs/architecture/lfm-platform-operations-uml.dediren/model.json
dediren project --input docs/architecture/lfm.dediren/model.json --plugin generic-graph --view technology-usage --target layout-request
dediren project --input docs/architecture/lfm.dediren/model.json --plugin generic-graph --view technology-usage --target render-metadata
dediren layout --plugin elk-layout --input <projection.json>
dediren validate-layout --input <layout.json>
dediren render --plugin svg-render --policy docs/architecture/lfm.dediren/render-policy.json --metadata <render-metadata.json> --input <layout.json> > <view>.render.json
jq -r '.data.content' <view>.render.json > <view>.svg
```

Repeat projection/layout/render for every view listed in the relevant package
manifest. `dediren render` emits a JSON result envelope; the SVG payload is
`.data.content` and should be extracted to the `.svg` path declared by the
project manifest. Copy reviewable SVG snapshots to the package-matching
subdirectory under [`renders/`](renders/) and keep the README view inventory,
render links, and source provenance aligned with the package.
