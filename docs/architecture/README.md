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

The run-signup UML package links upward to ArchiMate element ids
`sign-up-for-run`, `cancel-signup`, `runs-ui-entry`, `lfm-api`,
`run-management-service`, `cosmos-runs`, and `cosmos-raiders` through
`properties.uml.architecture_context`.

## Source Provenance

| ArchiMate layer | Lifted from |
|---|---|
| Application | [`lfm.sln`](../../lfm.sln), [`api/Lfm.Api.csproj`](../../api/Lfm.Api.csproj), [`app/Lfm.App.csproj`](../../app/Lfm.App.csproj), [`app/Lfm.App.Core/Lfm.App.Core.csproj`](../../app/Lfm.App.Core/Lfm.App.Core.csproj), [`shared/Lfm.Contracts/Lfm.Contracts.csproj`](../../shared/Lfm.Contracts/Lfm.Contracts.csproj), [`api/Program.cs`](../../api/Program.cs), [`api/Functions/`](../../api/Functions/), [`app/Lfm.App.Core/Services/`](../../app/Lfm.App.Core/Services/), [`app/Pages/`](../../app/Pages/) |
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
dediren validate --input docs/architecture/lfm-run-signup-uml.dediren/model.json
dediren validate --plugin generic-graph --profile uml --input docs/architecture/lfm-run-signup-uml.dediren/model.json
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
