# LFM Architecture

The canonical architecture source is now the dediren package
[`lfm.dediren/`](lfm.dediren/). It was extracted from the current .NET solution,
Azure Bicep infrastructure, GitHub Actions deploy workflows, and Blazor route/API
surface.

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

| File | Purpose |
|---|---|
| [`lfm.dediren/project.json`](lfm.dediren/project.json) | Project manifest and actual view list |
| [`lfm.dediren/model.json`](lfm.dediren/model.json) | Canonical source graph, relationships, source evidence, and view membership |
| [`lfm.dediren/render-policy.json`](lfm.dediren/render-policy.json) | SVG page, colors, decorators, and relationship markers |
| [`lfm.dediren/render-metadata.json`](lfm.dediren/render-metadata.json) | ArchiMate semantic type selectors for renderer notation |

`lfm.dediren/generated/` is reproducible tool output and ignored by git.

## Views

| # | View | Diagram kind | Render | Source scope |
|---|---|---|---|---|
| 1 | LFM - Application Cooperation | Application Cooperation | [`application-cooperation.svg`](renders/application-cooperation.svg) | `*.csproj`, `api/Functions/*.cs`, app-core clients, Blazor pages, Blizzard clients |
| 2 | LFM - Technology Usage | Technology Usage | [`technology-usage.svg`](renders/technology-usage.svg) | `infra/main.bicep`, `infra/modules/*.bicep`, `api/Program.cs` |
| 3 | LFM - Production Release Migration | Migration | [`production-release-migration.svg`](renders/production-release-migration.svg) | `.github/workflows/deploy*.yml`, `secrets-scan.yml`, `analyze-infra.yml`, `infra/**/*.bicep`, deployable app/API projects |
| 4 | Run Signup - Business Process Candidate | Business Process Cooperation | [`run-signup-process-candidate.svg`](renders/run-signup-process-candidate.svg) | `/runs` UI flow and `Runs*Function` endpoints |
| 5 | Run Signup - Service Realization | Service Realization | [`run-signup-service-realization.svg`](renders/run-signup-service-realization.svg) | run-signup process candidate, `Run Management` service, `Lfm.Api`, Function App, Cosmos containers |

## Source Provenance

| ArchiMate layer | Lifted from |
|---|---|
| Application | [`lfm.sln`](../../lfm.sln), [`api/Lfm.Api.csproj`](../../api/Lfm.Api.csproj), [`app/Lfm.App.csproj`](../../app/Lfm.App.csproj), [`app/Lfm.App.Core/Lfm.App.Core.csproj`](../../app/Lfm.App.Core/Lfm.App.Core.csproj), [`shared/Lfm.Contracts/Lfm.Contracts.csproj`](../../shared/Lfm.Contracts/Lfm.Contracts.csproj), [`api/Program.cs`](../../api/Program.cs), [`api/Functions/`](../../api/Functions/), [`app/Lfm.App.Core/Services/`](../../app/Lfm.App.Core/Services/), [`app/Pages/`](../../app/Pages/) |
| Technology | [`infra/main.bicep`](../../infra/main.bicep), [`infra/modules/`](../../infra/modules/), [`api/Program.cs`](../../api/Program.cs), [`api/host.json`](../../api/host.json) |
| Implementation & Migration | [`.github/workflows/deploy.yml`](../../.github/workflows/deploy.yml), [`.github/workflows/secrets-scan.yml`](../../.github/workflows/secrets-scan.yml), [`.github/workflows/analyze-infra.yml`](../../.github/workflows/analyze-infra.yml), [`.github/workflows/deploy-infra.yml`](../../.github/workflows/deploy-infra.yml), [`.github/workflows/deploy-app-build.yml`](../../.github/workflows/deploy-app-build.yml), [`.github/workflows/deploy-app.yml`](../../.github/workflows/deploy-app.yml) |
| Business Process candidates | [`app/Pages/RunsPage.razor`](../../app/Pages/RunsPage.razor), [`app/Lfm.App.Core/Services/RunsClient.cs`](../../app/Lfm.App.Core/Services/RunsClient.cs), [`api/Functions/RunsListFunction.cs`](../../api/Functions/RunsListFunction.cs), [`api/Functions/RunsDetailFunction.cs`](../../api/Functions/RunsDetailFunction.cs), [`api/Functions/RunsSignupFunction.cs`](../../api/Functions/RunsSignupFunction.cs), [`api/Functions/RunsSignupOptionsFunction.cs`](../../api/Functions/RunsSignupOptionsFunction.cs), [`api/Functions/RunsCancelSignupFunction.cs`](../../api/Functions/RunsCancelSignupFunction.cs) |

## Rendering And Validation

Use the bundled `dediren` runtime from the `souroldgeezer-architecture`
plugin. The canonical evidence loop is:

```bash
dediren validate --input docs/architecture/lfm.dediren/model.json
dediren project --input docs/architecture/lfm.dediren/model.json --plugin generic-graph --view application-cooperation --target layout-request
dediren layout --plugin elk-layout --input <projection.json>
dediren validate-layout --input <layout.json>
dediren render --plugin svg-render --policy docs/architecture/lfm.dediren/render-policy.json --metadata docs/architecture/lfm.dediren/render-metadata.json --input <layout.json> > <view>.render.json
jq -r '.data.content' <view>.render.json > <view>.svg
```

Repeat projection/layout/render for every view listed in
[`project.json`](lfm.dediren/project.json). `dediren render` emits a JSON
result envelope; the SVG payload is `.data.content` and should be extracted to
the `.svg` path declared by the project manifest. Copy reviewable SVG snapshots
to [`renders/`](renders/) and keep the README view inventory, render links, and
source provenance aligned with the package.
