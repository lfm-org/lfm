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
| `LFM_CLIENT_ID` | Yes | Blizzard OAuth client ID |
| `LFM_CLIENT_SECRET` | Yes | Blizzard OAuth client secret |
| `HMAC_SECRET` | Yes | 64 hex chars. Generate: `openssl rand -hex 32` |
| `SESSION_ENCRYPTION_KEY` | Yes | 64 hex chars. Generate: `openssl rand -hex 32` |
| `COSMOS_KEY_CONTENT` | Yes | Cosmos emulator master key (base64). Generate: `openssl rand -base64 32` |
| `COSMOS_ENDPOINT` | Yes | `http://127.0.0.1:8081` for the local emulator |
| `COSMOS_KEY` | Yes | Must match the key derived from `COSMOS_KEY_CONTENT` |
| `AzureWebJobsStorage` | Yes | Azurite connection string: `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;` |
| `BLOB_STORAGE_URL` | Yes | `http://127.0.0.1:10000/devstoreaccount1` |
| `PUBLIC_BLOB_STORAGE_URL` | Yes | Same as `BLOB_STORAGE_URL` for local |
| `APP_BASE_URL` | Default ok | `http://localhost:5138` (Blazor dev server port) |
| `BATTLE_NET_REDIRECT_URI` | Default ok | `http://localhost:7071/api/battlenet/callback` |
| `BATTLE_NET_REGION` | Default ok | `eu` |
| `BATTLE_NET_COOKIE_SECURE` | Default ok | `false` for local dev |
| `COOKIE_DOMAIN` | Default ok | `localhost` |
| `COSMOS_DATABASE` | Default ok | `lfm` |
| `KEY_VAULT_URL` | No | Leave empty for local dev |
| `TEST_MODE` | Default ok | `false` |

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

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release
```

Requires the Docker test stack from `docker-compose.test.yml`.

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
| `COOKIE_DOMAIN` | Cookie domain (parent domain with dot prefix) |

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
