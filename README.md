# LFM

LFM has three parts:

- `app/`: a Blazor WebAssembly single-page app
- `api/`: an Azure Functions backend (.NET 10 isolated)
- `infra/`: Azure infrastructure defined with Bicep

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (version pinned in `global.json`)
- [Docker](https://www.docker.com/) (for E2E tests and local stack)

## Run Locally

### 1. Install the pre-commit hook

```bash
cp scripts/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

### 2. Configure environment

```bash
cp example.env .env
```

Edit `.env` and fill in the required values. See the table below for details.

| Variable | Required | Notes |
|----------|----------|-------|
| `Blizzard__ClientId` | Yes | Blizzard OAuth client ID |
| `Blizzard__ClientSecret` | Yes | Blizzard OAuth client secret |
| `Blizzard__Region` | Default ok | `eu` |
| `Blizzard__RedirectUri` | Default ok | `http://localhost:7071/api/battlenet/callback` |
| `Blizzard__AppBaseUrl` | Default ok | `http://localhost:5138` (Blazor dev server port, no trailing slash) |
| `AZURE_FUNCTIONS_ENVIRONMENT` | Default ok | `Development`; required when `Audit__HashSalt` is empty |
| `Cors__AllowedOrigins__0` | Default ok | `http://localhost:5138` |
| `COSMOS_KEY_CONTENT` | Yes | Cosmos emulator master key (base64). Generate: `openssl rand -base64 32` |
| `Cosmos__Endpoint` | Default ok | `http://cosmosdb:8081` for the compose network |
| `Cosmos__AuthKey` | Yes | Must match the key derived from `COSMOS_KEY_CONTENT` |
| `Cosmos__DatabaseName` | Default ok | `lfm` |
| `Cosmos__ConnectionMode` | Default ok | `Gateway` for the Linux emulator |
| `AzureWebJobsStorage` | Yes | Azurite connection string: `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;` |
| `Storage__BlobConnectionString` | Yes | Same Azurite connection string as `AzureWebJobsStorage` |
| `Auth__CookieName` | Default ok | `battlenet_token` |
| `Auth__CookieMaxAgeHours` | Default ok | `24` |
| `Auth__KeyVaultUrl` | No | Leave empty for local dev |
| `PrivacyContact__Email` | Default ok | Privacy contact returned by the API |
| `Audit__HashSalt` | No | Empty logs plaintext actor IDs only in Development/E2E; deployed environments fail closed unless this is a resolved secret |

### 3. Start local stack

```bash
docker compose -f docker-compose.local.yml up
```

This starts:

- Cosmos emulator on `http://127.0.0.1:8081` (explorer on port 1234)
- Azurite blob storage on `http://127.0.0.1:10000`
- Azure Functions on `http://localhost:7071`

### 4. Run the Blazor app

```bash
dotnet run --project app/Lfm.App.csproj
```

The app serves on `http://localhost:5138`.

## Build and Test

```bash
dotnet restore lfm.sln
dotnet build lfm.sln -c Release
dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release
dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release
dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release
```

Format check:

```bash
dotnet format lfm.sln --verify-no-changes --no-restore --severity error
```

Bundle size gate (after publish):

```bash
dotnet publish app/Lfm.App.csproj -c Release -o ./publish/app
./scripts/check-bundle-size.sh ./publish/app/wwwroot 5
```

## E2E Tests

Optional drift check after route, selector, auth-helper, or seed-shape changes:

```bash
./scripts/check-e2e-drift.sh
```

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release
```

Requires a running Docker engine — Testcontainers spins up Cosmos + Azurite; the API and Blazor app are published and run in-process.

Maintenance guidance: see [E2E Maintenance](docs/testing/e2e-maintenance.md)
for lane selection, seed mutation rules, drift checks, and diagnostics
expectations.

## Deployment

Infrastructure is deployed via the `Deploy Infrastructure` GitHub Actions workflow. All Bicep parameters are passed inline from GitHub repository variables.

### Required GitHub Actions Variables

**Azure identity (OIDC):**

| Variable | Purpose |
|----------|---------|
| `AZURE_CLIENT_ID` | Service principal client ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

**Resource names:**

| Variable | Purpose |
|----------|---------|
| `AZURE_RESOURCE_GROUP` | Target resource group |
| `AZURE_LOCATION` | Azure region |
| `COSMOS_ACCOUNT_NAME` | Cosmos DB account name |
| `STORAGE_ACCOUNT_NAME` | Storage account name |
| `FUNCTION_APP_NAME` | Function App name |
| `SWA_NAME` | Static Web App name |
| `KEY_VAULT_NAME` | Key Vault name |
| `LOG_ANALYTICS_NAME` | Log Analytics workspace name |
| `COSMOS_DATABASE` | Cosmos DB database name |

**Domains:**

| Variable | Purpose |
|----------|---------|
| `API_HOSTNAME` | API custom domain |
| `FRONTEND_HOSTNAME` | Frontend custom domain |

**Application:**

| Variable | Purpose |
|----------|---------|
| `PRIVACY_EMAIL` | Privacy contact email; substituted into `/.well-known/security.txt` at build time |
| `BATTLE_NET_REGION` | Battle.net region code |

**Build-time template substitution** (used by the `GenerateStaticTemplates` MSBuild target):

| Variable | Purpose |
|----------|---------|
| `EXPIRES_SECURITY_TXT` | ISO-8601 timestamp when the `security.txt` contact expires (RFC 9116 requires this field) |
| `SECURITY_POLICY_URL` | URL to the deployer's `SECURITY.md` (e.g. `https://github.com/<you>/<repo>/blob/main/SECURITY.md`) |
| `API_HOSTNAME` | API custom domain; injected into the CSP `connect-src` and `img-src` allowlist in `staticwebapp.config.json` |
| `FRONTEND_HOSTNAME` | Frontend custom domain; injected into the `security.txt` `Canonical` field |
| `STORAGE_ACCOUNT_NAME` | Storage account name; used to build the blob endpoint in the CSP allowlist |

These five variables are set in the `dotnet publish` step of `deploy-app.yml`. `app/wwwroot/.well-known/security.txt` and `app/wwwroot/staticwebapp.config.json` are generated from their `.template` counterparts and gitignored.

`frontendOrigin`, `battleNetRedirectUri`, and resource `tags` are derived from the above in the workflow.

### Required GitHub Actions Secrets

| Secret | Purpose |
|--------|---------|
| `SWA_DEPLOYMENT_TOKEN` | Static Web Apps deployment token |

## Structure

- `app/`: Blazor WASM pages, components, services
- `app/Lfm.App.Core/`: framework-neutral services, i18n, auth (Stryker-mutatable)
- `api/Functions/`: Azure Function handlers
- `shared/`: shared models and contracts used by both app and api
- `tests/Lfm.Api.Tests/`: xUnit tests for API handlers
- `tests/Lfm.App.Tests/`: bUnit component tests for Blazor
- `tests/Lfm.App.Core.Tests/`: pure-logic unit tests (Stryker target)
- `tests/Lfm.E2E/`: Playwright .NET end-to-end tests
- `infra/main.bicep`: main infrastructure entry point

## API Contract

The source-of-truth OpenAPI 3.1 contract for the API lives at
[`api/openapi.yaml`](api/openapi.yaml). It is validated on every pull
request by `OpenApiContractTests` in `tests/Lfm.Api.Tests/Openapi/`,
which runs inside the existing `verify` CI gate — no separate lint
workflow, no Node toolchain. The file is intended to be consumed
directly by both the first-party SPA (type-generated clients) and
downstream AGPL operators who fork and self-host (machine-readable
contract without running the server).

The API does **not** serve a live schema in production. A future slice
will adopt `Microsoft.Azure.Functions.Worker.Extensions.OpenApi` in
development-only mode (gated on `ASPNETCORE_ENVIRONMENT == Development`)
so handler annotations stay synchronised with `api/openapi.yaml`
without exposing a Swagger UI or live-schema endpoint to production
traffic.

### Versioning and the /api/v1/ alias window

`/api/v1/…` is the canonical HTTP surface from Phase 7 onward. The
OpenAPI contract, the first-party SPA, and every new consumer target
`/api/v1/…`.

The original unprefixed routes (`/api/runs`, `/api/me`, etc.) are kept
as **aliases** — each canonical handler carries a second
`[Function("xxx-v1")]` declaration at `Route = "v1/…"` that delegates
to the same method, so both route tables hit identical code. The
alias is a migration aid: forks or older SPA bundles that predate the
cut-over keep working during a deploy window. The legacy routes will
be removed in a follow-up release once App Insights traffic on the
unprefixed paths is effectively zero. See
[`docs/api-versioning.md`](docs/api-versioning.md) for the rollout
design, retirement criteria, and why two `[Function]` declarations
beats middleware-level route rewriting for the Azure Functions
isolated worker.

## Notes

Use standard C# conventions: 4-space indentation. Run `dotnet format` before committing. Keep commits short and imperative.

## License

This project is licensed under the **GNU Affero General Public License
v3.0 or later** (AGPL-3.0-or-later).

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](LICENSE)

- [`LICENSE`](LICENSE) — full license text
- [`NOTICE`](NOTICE) — copyright notice and "how to apply" pointer
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — inbound=outbound policy for contributions
- [`REUSE.toml`](REUSE.toml) — collective license coverage for files that cannot carry SPDX headers

<!-- REUSE-IgnoreStart -->
Every source file carries an `SPDX-License-Identifier: AGPL-3.0-or-later`
header.
<!-- REUSE-IgnoreEnd -->
The REUSE CI job
(`.github/workflows/license-compliance.yml`) verifies compliance on
every pull request.

Dependency licenses are verified by
`.github/workflows/dep-license-check.yml` against the allowlist at
`.github/license-allowlist.txt`. The one-time baseline audit is in
`docs/security-reviews/2026-04-16-dep-license-audit.md`.

## AI Assistance

This project is developed with the assistance of [Claude Code](https://claude.ai/claude-code) and [Codex](https://openai.com/codex/). AI is used for coding, code review, and documentation tasks throughout the codebase.
