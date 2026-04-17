# LFM

LFM has three parts:

- `app/`: a Blazor WebAssembly single-page app
- `api/`: an Azure Functions backend (.NET 10 isolated)
- `infra/`: Azure infrastructure defined with Bicep

## Prerequisites

**Local development:**

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (version pinned in `global.json`)
- [Docker](https://www.docker.com/) (for E2E tests and local stack)

**Deployment (Azure):**

- Azure subscription with Contributor access on a resource group
- [Azure CLI (`az`)](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli)
- A [Battle.net developer account](https://develop.battle.net/access/clients) to create an OAuth application
- A registered domain for API + frontend hostnames (optional — Azure default hostnames work for non-production)

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
| `COSMOS_KEY` | Yes | Base64-encoded key derived from `COSMOS_KEY_CONTENT`: `COSMOS_KEY=$(echo -n "$COSMOS_KEY_CONTENT" | base64 -w 0)` |
| `AzureWebJobsStorage` | Yes | Azurite connection string: `DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;` |
| `BLOB_STORAGE_URL` | Yes | `http://127.0.0.1:10000/devstoreaccount1` for local emulator; `https://<STORAGE_ACCOUNT_NAME>.blob.core.windows.net` in production |
| `PUBLIC_BLOB_STORAGE_URL` | Yes | Same as `BLOB_STORAGE_URL` for local; `https://<STORAGE_ACCOUNT_NAME>.blob.core.windows.net` in production (may differ from `BLOB_STORAGE_URL` if using a private endpoint for internal access) |
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

Start the Docker test stack first, then run the tests:

```bash
docker compose -f docker-compose.test.yml up -d
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release
```

The test stack (`docker-compose.test.yml`) provides isolated Cosmos, Azurite, Functions, and app containers. Copy `.env` to configure the stack (same variables as local dev).

## Deployment

Two GitHub Actions workflows manage production:

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| `Deploy Infrastructure` (`deploy-infra.yml`) | Manual (`workflow_dispatch`) | Provisions Cosmos DB, Storage, Function App, Static Web App, Key Vault, and Log Analytics via Bicep |
| `Deploy App` (`deploy-app.yml`) | Push to `main` / manual | Builds and publishes the Functions API and Blazor SPA; pushes SPA bundle to Static Web Apps |

Run `Deploy Infrastructure` before `Deploy App` on first setup. `Deploy App` depends on `SWA_DEPLOYMENT_TOKEN`, which is only available after the SWA resource is provisioned (see [Deploying your own LFM instance](#deploying-your-own-lfm-instance) below).

All Bicep parameters are passed inline from GitHub repository variables.

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
| `COOKIE_DOMAIN` | Cookie domain with a leading dot, e.g. `.yourdomain.tld` (required for subdomain cookie sharing — omitting the dot breaks auth) |

**Application:**

| Variable | Purpose |
|----------|---------|
| `PRIVACY_EMAIL` | Privacy contact email; substituted into `/.well-known/security.txt` at build time |
| `BATTLE_NET_REGION` | Battle.net region code |

**Build-time template substitution** (used by the `GenerateStaticTemplates` MSBuild target):

| Variable | Purpose |
|----------|---------|
| `EXPIRES_SECURITY_TXT` | ISO-8601 timestamp when the `security.txt` contact expires (RFC 9116 §2.5.5 requires this field; must be within one year — update and redeploy annually) |
| `SECURITY_POLICY_URL` | URL to the deployer's `SECURITY.md` (e.g. `https://github.com/<you>/<repo>/blob/main/SECURITY.md`); create or update `SECURITY.md` in your fork before deploying |
| `API_HOSTNAME` | API custom domain; injected into the CSP `connect-src` and `img-src` allowlist in `staticwebapp.config.json` |
| `FRONTEND_HOSTNAME` | Frontend custom domain; injected into the `security.txt` `Canonical` field |
| `STORAGE_ACCOUNT_NAME` | Storage account name; used to build the blob endpoint in the CSP allowlist |

These five variables are set in the `dotnet publish` step of `deploy-app.yml`. `app/wwwroot/.well-known/security.txt` and `app/wwwroot/staticwebapp.config.json` are generated from their `.template` counterparts and gitignored.

`frontendOrigin`, `battleNetRedirectUri`, and resource `tags` are derived from the above in the workflow.

### Required GitHub Actions Secrets

| Secret | Purpose |
|--------|---------|
| `SWA_DEPLOYMENT_TOKEN` | Static Web Apps deployment token — available only after `Deploy Infrastructure` creates the SWA resource. Retrieve with: `az staticwebapp secrets list --name <SWA_NAME> --resource-group <AZURE_RESOURCE_GROUP> --query properties.apiKey -o tsv` |

## Deploying your own LFM instance

This project is AGPL-3.0-or-later. You are welcome to deploy your own instance. Every deployer maintains their own infrastructure, secrets, domain, and privacy contact.

### Step 1: Fork the repo

Fork on GitHub, then clone your fork:

```bash
git clone git@github.com:<your-user>/<your-fork>.git
cd <your-fork>
```

Update `SECURITY_POLICY_URL` in `example.env` (and your GitHub variable) to point to your fork's `SECURITY.md`.

### Step 2: Register a Battle.net OAuth application

At <https://develop.battle.net/access/clients>, create a new client. Register these redirect URIs:

- `http://localhost:7071/api/battlenet/callback` (local development)
- `https://<API_HOSTNAME>/api/battlenet/callback` (production — use the hostname you will set in Step 4)

Note the **Client ID** and **Client Secret** — you will seed these into Key Vault in Step 6.

### Step 3: Create an Azure AD app with GitHub OIDC federation

The deploy pipeline uses GitHub OIDC (no long-lived secret) to authenticate to Azure:

1. In the Azure portal, go to **Microsoft Entra ID → App registrations → New registration**. Give it a name such as `lfm-<your-fork>-github`. Note the **Application (client) ID** and **Directory (tenant) ID**.

2. On the new app, go to **Certificates & secrets → Federated credentials → Add credential** and configure:
   - **Issuer:** `https://token.actions.githubusercontent.com`
   - **Subject identifier:** `repo:<your-user>/<your-fork>:ref:refs/heads/main`
   - **Audience:** `api://AzureADTokenExchange`

3. Add a second federated credential for pull request workflows:
   - **Subject identifier:** `repo:<your-user>/<your-fork>:pull_request`

4. Grant the app **Contributor** on your target resource group (create the resource group first if it does not exist):

   ```bash
   az role assignment create \
     --assignee <AZURE_CLIENT_ID> \
     --role Contributor \
     --scope /subscriptions/<AZURE_SUBSCRIPTION_ID>/resourceGroups/<AZURE_RESOURCE_GROUP>
   ```

Reference: <https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure>.

### Step 4: Configure GitHub repository variables and secrets

In your fork, go to **Settings → Secrets and variables → Actions** and add the following. Refer to `## Deployment` above for a complete annotated variable table.

**Repository variables** (Settings → Variables):

| Variable | Example value |
|----------|--------------|
| `AZURE_CLIENT_ID` | Application (client) ID from Step 3 |
| `AZURE_TENANT_ID` | Directory (tenant) ID from Step 3 |
| `AZURE_SUBSCRIPTION_ID` | your Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | `lfm-myfork-rg` |
| `AZURE_LOCATION` | `westeurope` |
| `COSMOS_ACCOUNT_NAME` | `lfm-myfork-cosmos` (globally unique) |
| `STORAGE_ACCOUNT_NAME` | `lfmmyforkst` (globally unique, 3–24 chars, lowercase alphanumeric) |
| `FUNCTION_APP_NAME` | `lfm-myfork-fn` (globally unique) |
| `SWA_NAME` | `lfm-myfork-swa` |
| `KEY_VAULT_NAME` | `lfm-myfork-kv` (globally unique) |
| `LOG_ANALYTICS_NAME` | `lfm-myfork-law` |
| `COSMOS_DATABASE` | `lfm` |
| `API_HOSTNAME` | `api.yourdomain.tld` (or the Azure default after provisioning) |
| `FRONTEND_HOSTNAME` | `app.yourdomain.tld` (or the Azure default) |
| `COOKIE_DOMAIN` | `.yourdomain.tld` (leading dot required — see `## Deployment`) |
| `BATTLE_NET_REGION` | `eu`, `us`, `kr`, or `tw` |
| `PRIVACY_EMAIL` | your privacy contact email |
| `EXPIRES_SECURITY_TXT` | `2027-04-17T00:00:00Z` (update annually — RFC 9116 requires within one year) |
| `SECURITY_POLICY_URL` | `https://github.com/<your-user>/<your-fork>/blob/main/SECURITY.md` |

**Repository secret** (Settings → Secrets):

| Secret | When to add |
|--------|------------|
| `SWA_DEPLOYMENT_TOKEN` | After the first `Deploy Infrastructure` run (see Step 5) |

### Step 5: First deploy — two runs required

The initial setup requires two separate workflow runs because the SWA deployment token is only available after the infrastructure exists:

**Run 1 — infrastructure:**

Trigger the `Deploy Infrastructure` workflow from the **Actions** tab (select the workflow → Run workflow). This provisions Cosmos DB, Storage, Function App, Static Web App, Key Vault, and Log Analytics.

**Retrieve the SWA token and add it as a secret:**

```bash
az staticwebapp secrets list \
  --name <SWA_NAME> \
  --resource-group <AZURE_RESOURCE_GROUP> \
  --query properties.apiKey -o tsv
```

Add the output as the repository secret `SWA_DEPLOYMENT_TOKEN`.

**Run 2 — app deploy:**

Trigger the `Deploy App` workflow (or push to `main`). This builds and publishes the Functions API and Blazor SPA and deploys the SPA to Static Web Apps.

### Step 6: Seed Key Vault with Battle.net credentials

The Functions runtime reads Battle.net credentials from Key Vault at startup, not from GitHub variables. Add them with the Azure CLI:

```bash
az keyvault secret set \
  --vault-name <KEY_VAULT_NAME> \
  --name Blizzard--ClientId \
  --value "<battle.net client ID from Step 2>"

az keyvault secret set \
  --vault-name <KEY_VAULT_NAME> \
  --name Blizzard--ClientSecret \
  --value "<battle.net client secret from Step 2>"
```

The Function App's managed identity is granted `Key Vault Secrets User` automatically by the Bicep template. Restart the Function App to pick up the new secrets:

```bash
az functionapp restart \
  --name <FUNCTION_APP_NAME> \
  --resource-group <AZURE_RESOURCE_GROUP>
```

### Step 7: Verify the deployment

- `https://<API_HOSTNAME>/api/health/ready` — should return HTTP 200
- `https://<FRONTEND_HOSTNAME>/.well-known/security.txt` — should list your `PRIVACY_EMAIL`
- `https://<FRONTEND_HOSTNAME>/` — should load the Blazor SPA
- Signing in at `https://<FRONTEND_HOSTNAME>/` redirects through Battle.net OAuth

### Step 8: Ongoing maintenance

- **`SWA_DEPLOYMENT_TOKEN`** is a long-lived static token. Rotate quarterly by running `az staticwebapp secrets reset-api-key --name <SWA_NAME> --resource-group <AZURE_RESOURCE_GROUP>`, then updating the GitHub secret.
- **`EXPIRES_SECURITY_TXT`** must be updated and the app redeployed at least annually (RFC 9116 §2.5.5).
- NuGet dependency updates are automated via Dependabot PRs already configured in the repo.

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
