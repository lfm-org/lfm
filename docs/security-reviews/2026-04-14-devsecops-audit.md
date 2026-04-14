# DevSecOps Audit — 2026-04-14

Deep-mode audit against `docs/security-reference/devsecops.md`. Rubric codes are cited from `.claude/skills/devsecops-audit/references/smell-catalog.md`.

Repo: `tommimarkus/sisu-raidcal` (local path `/home/souroldgeezer/repos/lfm`).
Auditor: Claude Code (Opus 4.6), MCP-augmented static audit.
Mode: **deep**. Cost stance: **free** (source: `skills/devsecops-audit/config.yaml`). Band 2 smells suppressed for `bicep`.

Extensions loaded: `github-actions`, `bicep`, `dockerfile`, `dotnet-security`.

---

## 1. Scope statement

In scope:

- All `.github/workflows/*.yml` (ci, e2e, deploy, deploy-app, deploy-infra, analyze-infra, secrets-scan, stryker-nightly).
- `infra/main.bicep` and every module under `infra/modules/`.
- `api/Dockerfile`, `docker-compose.local.yml`, `docker-compose.test.yml`.
- `api/` (Azure Functions .NET isolated): `Program.cs`, `Middleware/*`, `Auth/*`, `Functions/*`, `Options/*`, `Services/*`.
- `app/wwwroot/staticwebapp.config.json`.
- `CLAUDE.md`, `scripts/pre-commit`, `.gitattributes`, `.github/dependabot.yml`.
- Live GitHub state for `tommimarkus/sisu-raidcal` via MCP: branches, workflow runs, releases, PRs, alerts.

Out of scope (by construction):

- All `.worktrees/**` — working copies, not shipped state.
- Deep mutation/reachability analysis.
- Battle.net upstream dependency review (trust the supplier; we don't build it).
- Contents of GitHub repo Variables / Secrets (not introspectable via MCP).

---

## 2. Declared target levels

No explicit declaration found for any of: ASVS, SCVS, SLSA Build Level, DSOMM, SAMM, NIS2 applicability, CRA applicability. `SECURITY.md` is absent, and `README.md` / `docs/` contain no security-target banner.

Finding: **`DSO-HC-0`** — no declared target levels.
- severity: `warn`
- rubric: §9 item 2
- action: Declare a minimum target in a new `SECURITY.md` at repo root. For a hobby project of this shape, a reasonable starting point is **ASVS L1** (web app) + **SCVS L1** (supply chain) + **SLSA Build L1** (declared, even if not fully evidenced). The act of writing the target is itself the first enforcing step — you can't audit against `L?`.

---

## 3. Stage coverage matrix

| Stage | Controls present | Enforcing | Decorative | Missing |
|---|---|---|---|---|
| **Design** | CLAUDE.md architectural guidance; `docs/security-reference/devsecops.md` rubric; `docs/security-reviews/` historical reviews | Guidance documents drive new work (reviewed in-conversation) | — | Threat model document per interface; STRIDE on trust boundaries (Battle.net OAuth, session cookie crypto, Cosmos partition-key auth); abuse cases |
| **Develop** | `scripts/pre-commit` blocks `.env`/PEM/etc; `.gitattributes` blocks same; Dependabot weekly; `dotnet format` enforced in CI | `dotnet format` CI gate (ci.yml:52), Dependabot update PRs | `scripts/pre-commit` (opt-in; never installed by CI or enforced) | `CODEOWNERS`; signed commits; declarative branch-protection config (`.github/rulesets/*`) |
| **Build** | `dotnet list package --vulnerable` job in ci.yml:107 and deploy-app.yml:42; gitleaks in secrets-scan.yml; PSRule for Azure in analyze-infra.yml; bundle-size regression gate | Dependency audit (exit 1 on any vulnerable package — hard gate); gitleaks full-history scan; format/build/test gates | PSRule SARIF output uploaded as artifact only (no Code Scanning ingest → nobody reads it across runs) | NuGet lockfile (`packages.lock.json`); SBOM generation (CycloneDX/SPDX); artifact signing; SLSA provenance attestation |
| **Test** | xUnit (api + app + app.core); bUnit; Playwright E2E against docker-compose stack; Stryker.NET mutation nightly against App.Core | Unit + component tests block `deploy-app` (it re-runs them); Stryker nightly provides a quality floor; E2E gated behind `#if E2E` + env-var check in `E2ELoginFunction.cs` | E2E `workflow_dispatch`-only with no success recovery after 2026-04-13 failure (per MCP runs) | DAST against staging; authenticated API scan; CSP / CORS / `frame-ancestors` browser assertion (see test-quality-audit E sub-lane S) |
| **Release** | Per-push `Deploy` workflow → Azure Functions + SWA | Health-check gate post-deploy (deploy-app.yml:90) | — | Signed artifact (`DSO-HC-11`); SLSA provenance; SBOM attached to release; **GitHub Releases are empty** (`list_releases` returned `[]`) |
| **Deploy** | OIDC federation for Functions (gha.POS-2) via `azure/login@<sha>`; Bicep deploys under the same OIDC identity; health check probe | Functions deploy OIDC identity with no client-secret; Bicep `what-if` preview step; infra module-level locks | Alerts deploy step has `continue-on-error: true` (deploy-infra.yml:187, labeled "tolerates first-run failure" — functionally decorative first time) | OIDC for SWA — falls back to `secrets.SWA_DEPLOYMENT_TOKEN` long-lived token |
| **Operate** | App Insights workspace-linked; diagnostic settings on Cosmos/Storage/KeyVault/Functions/SWA → Log Analytics (1 GB/day cap under 5 GB free grant); alerts.bicep exists | Diagnostic settings configured per CLAUDE.md Infrastructure Development checklist; App Insights `DisableLocalAuth: true` forcing AAD for telemetry | Alerts "tolerate first-run failure" — whether they currently exist in deployed state is unverifiable without `az monitor` | CI/CD audit events forwarded to SIEM (§5.1 item 12); anomaly detection on pipeline runs; per-severity remediation SLA tracked with aging cohorts |
| **Respond** | `docs/security-reviews/` historical review track | Historical reviews feed back into code via documented change log | — | `SECURITY.md` with disclosure contact + triage SLA; `.well-known/security.txt`; incident runbook linking "exploitation report → triage → patch → ship" |

Stage verdict pattern: **Build is the strongest stage** (enforcing dependency audit, secret scanning, IaC PSRule). **Develop, Release, and Respond are the weakest** — no branch protection, no release artifacts, no disclosure channel.

Live-state note: because MCP confirms `main` is unprotected (§10 Probe 1), every control under **Develop** that depends on PR-review enforcement degrades to "static config only, runtime bypassable" — a solo committer can push directly to `main` with zero gates.

---

## 4. Anti-pattern scan (OWASP CI/CD Top 10)

One finding per observed anti-pattern. Codes map via `references/smell-catalog.md` §"CICD-SEC-* — detection hints".

### `CICD-SEC-1` — Insufficient Flow Control Mechanisms
- severity: `warn` (was `block`; see compensating-controls note below)
- evidence: MCP `list_branches` returns `main` with `protected: false` (Probe 1 result in §10). No `.github/rulesets/*` or `.github/branch-protection.yml` declarative config. No `CODEOWNERS` at repo root, `.github/`, or `docs/`.
- parent smell: `DSO-HC-4`
- platform constraint: repo is a private repo on GitHub Free. Branch protection / ruleset **enforcement** is not available on that plan (the configuration UI may appear but rules do not gate pushes). Upgrading is out of scope per CLAUDE.md § Cost Guidance.
- compensating controls (accept finding, document in `SECURITY.md`):
  1. **Move all security gates inline with the deploy path.** Today `deploy-app.yml` runs build, test, and `dotnet list package --vulnerable` — these **do** block production. But `secrets-scan.yml` (gitleaks) and `analyze-infra.yml` (PSRule) run in **parallel** workflows and a failing gitleaks run does not stop `deploy.yml`. Restructure so the deploy workflow waits on these. Options: (a) turn `secrets-scan.yml` and `analyze-infra.yml` into `workflow_call` jobs and `needs:`-depend on them from `deploy.yml`; (b) inline the gitleaks/PSRule steps as the first steps of `deploy.yml`. Either way, a secret leak or IaC misconfiguration lands in main but **does not deploy**.
  2. **Require signed commits locally.** `git config --global commit.gpgsign true` + a signing key. GitHub will show "Verified" badges. Prevents a stolen PAT from pushing unsigned commits that match your identity.
  3. **Strong personal-account MFA** (hardware key preferred). Protects the identity that can push directly to main.
  4. **Client-side pre-push hook** that runs `dotnet format --verify-no-changes`, `dotnet build`, and `dotnet list package --vulnerable` locally, rejecting the push on failure. This is a discipline layer, not a security boundary — a determined attacker who controls the workstation can bypass it — but it catches accidental direct-push-of-broken-code. Add to `scripts/pre-push` alongside the existing `scripts/pre-commit`.
  5. **Add a `CODEOWNERS` file anyway.** Without branch protection it is advisory, but it documents ownership and surfaces in GitHub's PR UI if you ever do open one. Zero cost.
  6. **Document the gap.** In `SECURITY.md`, a short "Known limitations" section listing "no pre-merge branch protection on current GitHub plan; all gates enforced at deploy time via `deploy.yml`." This converts `DSO-HC-4` from "silently missing" to "knowingly accepted with compensating controls" — the rubric §9 distinction between presence and efficacy treats these differently.

Net effect of (1) + (2) + (3) + (6): `CICD-SEC-1` remains a `warn` (not `block`) because the deploy path is still a genuine exit-1 gate for the subset of findings that run inline; the unclosed gap is "malicious commit that passes all automated checks" — a gap that branch protection would also not close, since an approver's eyes would still be the only thing between a subtly-malicious commit and production.

### `CICD-SEC-2` — Inadequate IAM
- severity: `info`
- evidence: Only `Deploy to Azure Static Web Apps` (deploy-app.yml:114-120) uses a static token (`SWA_DEPLOYMENT_TOKEN` GitHub secret). Everything else (Functions deploy, Bicep deploy) authenticates via OIDC federation with no `client-secret`.
- parent smell: `DSO-HC-7` (scoped to the SWA token only)
- action: Accept as a known Microsoft platform limitation (SWA's deploy action does not yet support OIDC). Document the exception in `SECURITY.md` with a rotation cadence (e.g. quarterly), and track the OIDC-for-SWA product feature. When Microsoft adds OIDC support, the exception closes.

### `CICD-SEC-3` — Dependency Chain Abuse
- severity: `warn`
- evidence:
  - No `packages.lock.json` anywhere (`**/packages.lock.json` → no files), no `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` in any `*.props`. `dotnet restore` is floating within `<PackageReference Version="...">` ranges on every build.
  - `api/Dockerfile:1` and `api/Dockerfile:12` use tag-pinned `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0` — no `@sha256:` digest.
  - `docker-compose.local.yml:3,20` and `docker-compose.test.yml:3,20` use unpinned MCR tags `cosmosdb-linux-azure-cosmos-emulator:vnext-preview` and `azure-storage/azurite` (no tag, meaning `:latest`).
- parent smells: `DSO-HC-2`, `docker.HC-2`, `DSO-SUB-1` (hash-pinned-but-unverified class)
- mitigating controls: `dotnet list package --vulnerable --include-transitive` runs in `ci.yml:107` and `deploy-app.yml:42`, exiting `1` on any vulnerable transitive. Dependabot weekly updates `nuget` and `github-actions` ecosystems with a `dotnet-minor` group.
- action (smallest diff first): add `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to a `Directory.Build.props` at repo root, run `dotnet restore lfm.sln`, commit the generated `packages.lock.json` files. Then pin Dockerfile `FROM` to `@sha256:` digests (take current digest from `docker image inspect`). Under the dockerfile extension's `mcr.microsoft.com` carve-out the MCR `FROM`s are `warn` not `block`, so this is a quality improvement not a blocker.

### `CICD-SEC-4` — Poisoned Pipeline Execution
- severity: `info` (no Direct-PPE observed)
- evidence: no `pull_request_target` trigger anywhere. `deploy.yml:69` assigns `inputs.stage` to an env var `STAGE` before shelling out (`run: if [ "$STAGE" = "all" ] ...`), which is the canonical `gha.HC-6` defence pattern and a **positive signal**. Reusable workflow calls (`./.github/workflows/deploy-infra.yml`, `./.github/workflows/deploy-app.yml` in deploy.yml:92,101) are local refs, not external template imports → no Indirect-PPE vector.
- action: none. Keep the env-var sanitization pattern when adding future `workflow_dispatch` inputs.

### `CICD-SEC-5` — Insufficient Pipeline-Based Access Controls
- severity: `info`
- evidence: every workflow declares a top-level `permissions:` block (`ci.yml:18`, `analyze-infra.yml:12`, `e2e.yml:16`, `stryker-nightly.yml:8`, `deploy-infra.yml:7`, `deploy-app.yml:7`, `deploy.yml:29`, `secrets-scan.yml:10`). Non-deploy workflows run with `contents: read`. Deploy workflows escalate to `id-token: write, contents: read` only. No workflow declares `write-all`.
- positive signals: `gha.POS-1`, `gha.POS-2`.
- action: none.

### `CICD-SEC-6` — Insufficient Credential Hygiene
- severity: `warn`
- evidence: **one** long-lived static token: `SWA_DEPLOYMENT_TOKEN` (deploy-app.yml:117). All other cloud auth is OIDC. Secret scanning via Gitleaks runs on every push and PR (`secrets-scan.yml:27`) — enforcing at the CI boundary. Pre-commit hook `scripts/pre-commit` blocks `.env`/PEM/keystores/sqlite, but is **opt-in**: the top-of-file comment says `Install: cp scripts/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit`.
- parent smells: `DSO-HC-7` (single credential), `DSO-POS-9` partially satisfied (server-side via gitleaks, opt-in pre-commit — so the "two layers" is really one-and-a-half)
- action: see `CICD-SEC-2` for the SWA token. For `scripts/pre-commit`, either install automatically via a `git init`-time hook script, or add a CI job that fails when `.git/hooks/pre-commit` content drift is detected on a dev machine (this is not really feasible in CI — just accept that it's opt-in and mark it as opt-in in CLAUDE.md).

### `CICD-SEC-7` — Insecure System Configuration
- severity: `info`
- evidence: no self-hosted runners; no public CI dashboards; GitHub Actions with pinned action SHAs eliminates the "outdated plugin" vector for the CI platform itself. `disableLocalAuth` enforced on Cosmos, App Insights, and Storage shared keys. Basic auth and FTP explicitly disabled on Functions via `basicPublishingCredentialsPolicies` (`functions.bicep:204-214`).
- action: none.

### `CICD-SEC-8` — Ungoverned Third-Party Services
- severity: `info`
- evidence: 27 `uses:` declarations across 8 workflows. **All** pinned to 40-char commit SHAs (verified by two-pass scan: every `uses:` ref tail is 40-hex). Publishers: `actions/*`, `azure/*`, `Azure/*`, `microsoft/ps-rule`, `dorny/paths-filter`. Count under 10 distinct third-party publishers → under the `DSO-LC-4` threshold of 20.
- positive signal: `gha.POS-3`.
- action: none.

### `CICD-SEC-9` — Improper Artifact Integrity Validation
- severity: `warn`
- evidence: `Deploy to Azure Functions` (deploy-app.yml:84) publishes `./publish/api` via `Azure/functions-action@<sha>` with **no intermediate artifact, no signing step, no SLSA provenance attestation, no SBOM generation**. `Deploy to Azure Static Web Apps` (deploy-app.yml:114) uploads `./publish/app/wwwroot` directly — also unsigned. `list_releases` returned `[]` — there is no GitHub Release to attach evidence to.
- parent smell: `DSO-HC-11`
- action: at minimum, generate a CycloneDX SBOM per deploy (there's `dotnet-cli` CycloneDX task or `cyclonedx/gh-dotnet-generate-sbom@<sha>`). Attach to a dated `gh release`. Signing via cosign-keyless is free and fits the stance. SLSA L2 via `slsa-framework/slsa-github-generator` is also free. These changes move the project from `DSO-HC-11` (unsigned) to `DSO-POS-1` + `DSO-POS-6` + `DSO-POS-16` — three positives for one diff. (This is the highest-leverage single change in the audit.)

### `CICD-SEC-10` — Insufficient Logging and Visibility
- severity: `warn`
- evidence: Azure-side observability is comprehensive — every resource has a `Microsoft.Insights/diagnosticSettings` sub-resource (cosmos.bicep:147, functions.bicep:216, keyvault.bicep:48, storage.bicep:70, swa.bicep:21), targeting a workspace with 1 GB/day cap. App Insights `DisableLocalAuth: true`. **But**: CI/CD events (workflow run logs, who triggered what, exit codes) are **not** forwarded off GitHub. A compromised CI environment could edit its own logs. Additionally MCP confirms **Dependabot alerts are disabled** at the repo level (403 "Dependabot alerts are disabled") and **Code scanning is not enabled** (403 "Code scanning is not enabled"). The PSRule SARIF from `analyze-infra.yml:30` is uploaded as a workflow artifact but not ingested into Code Scanning, so it is read by nobody between runs.
- parent smells: `DSO-HC-12`, `DSO-HC-15`, `DSO-SUB-6`
- action: (1) Enable Dependabot alerts in repo settings (free for public and private repos — zero cost). (2) Enable Code Scanning default setup (free for public repos; free CodeQL for private-but-under-org-plan). Re-route `analyze-infra.yml` to upload SARIF via `github/codeql-action/upload-sarif@<sha>` so PSRule findings land in the Code Scanning tab with aging. (3) Forwarding CI/CD events off-platform is a higher-cost control — defer under the `free` cost stance but document as a known gap.

---

## 5. Smell matches

Findings collected and de-duplicated from the anti-pattern scan and the extension walks. Each cites its rubric code; the full rule text lives in the catalog, not here.

### High-confidence (block / warn)

| Code | Location | Severity | Parent anti-pattern |
|---|---|---|---|
| `DSO-HC-4` | MCP probe `list_branches`: `main.protected = false` | block | `CICD-SEC-1` |
| `DSO-HC-10` | No `SECURITY.md`; no `.well-known/security.txt` under `app/wwwroot/` | block | — |
| `DSO-HC-11` | `deploy-app.yml:84-119` deploys unsigned `./publish/api` + `./publish/app/wwwroot` | warn | `CICD-SEC-9` |
| `DSO-HC-12` + `DSO-HC-15` | No Code Scanning, no Dependabot alerts enabled (MCP 403), PSRule SARIF artifact-only | warn | `CICD-SEC-10` |
| `DSO-HC-7` | `deploy-app.yml:117` `secrets.SWA_DEPLOYMENT_TOKEN` (scoped to SWA only) | warn | `CICD-SEC-2` / `CICD-SEC-6` |
| `DSO-HC-2` | No `packages.lock.json` for NuGet graph; `api/Dockerfile:1,12` tag-pinned `FROM` (MCR) | warn | `CICD-SEC-3` |
| `docker.HC-1` | `api/Dockerfile` — no `USER` directive, relies on base image default | warn | OWASP Docker Cheat Sheet |
| `docker.HC-2` | `api/Dockerfile:1,12`, `docker-compose.local.yml:3,20`, `docker-compose.test.yml:3,20` — no `@sha256:` digest. MCR carve-out downgrades block→warn. | warn | `CICD-SEC-3` |

### Low-confidence (warn / info) and subtle

| Code | Location | Severity |
|---|---|---|
| `DSO-HC-0` | No ASVS/SCVS/SLSA/DSOMM/SAMM/NIS2/CRA declaration anywhere | warn |
| `DSO-SUB-7` | CLAUDE.md mentions WAF pillars, but no per-release evidence pointer links a deploy to a specific pillar check | info |
| `DSO-SUB-8` (mild) | `e2e.yml:7` filters run to paths touching `api|app|shared|tests|lfm.sln|global.json` on pull requests. Without branch protection this is advisory; gitleaks runs on both `push: main` and `pull_request: main` (good), CI runs only on `pull_request: main` — a push directly to main skips CI. With `DSO-HC-4` unresolved, the solo-developer pattern of "push straight to main" bypasses CI entirely. | warn |
| `DSO-LC-9` inverse | Dependabot configured (`dependabot.yml`) but alerts feature is **disabled** at repo level. The update bot runs; the vulnerability alerting does not. Mitigated by `Audit dependencies` CI step. | warn |
| `dns.HC-5` inverse | `api/Middleware/SecurityHeadersMiddleware.cs:22-26` sets full header set; `staticwebapp.config.json:42-46` also sets globalHeaders. Twice-configured is fine, but no HSTS header is declared in SWA `globalHeaders` (relies on SWA platform default). | info |

### Positively **not** fired (worth stating for completeness)

| Code | Why it didn't fire |
|---|---|
| `dns.HC-4` | `api/Program.cs:56` uses `.WithOrigins(corsOpts.AllowedOrigins).AllowCredentials()` — explicit origin list, not `AllowAnyOrigin`. Correct. |
| `gha.HC-3` | No `pull_request_target` trigger anywhere. |
| `gha.HC-5` | The only `continue-on-error: true` occurrence is `deploy-infra.yml:187` — the alerts deploy step, **not** a security scan. Does not satisfy `gha.HC-5`'s "security scan" precondition. |
| `gha.HC-6` | `deploy.yml:65-87` moves `inputs.stage` through the `env:` block (`STAGE: ${{ inputs.stage }}`) and references `$STAGE` in the shell. Canonical mitigation. |
| `bicep.HC-1..HC-10` | Every bicep module checked — see §6 for positive-signal evidence. |
| `DSO-HC-1` | Gitleaks returns no hits in CI; no real-shape credential in committed `appsettings*.json` (Blizzard secret is a Key Vault reference). |
| `DSO-HC-13` | Dockerfile carve-out: no `USER root` set explicitly; finding is on "no `USER` directive at all" = `docker.HC-1` above, not `DSO-HC-13` (which is a narrower form). |
| `DSO-HC-16` | HSTS set in both API middleware and SWA expected platform default; no mixed-content auth vector. |
| `DSO-HC-20` | No `AllowAnyOrigin()` anywhere. |
| `dns.HC-3` | Every non-public function has `[RequireAuth]` on method or class; `AuthPolicyMiddleware` short-circuits with 401 before the function body runs. `BattleNetLoginFunction.cs:11` has a self-documenting comment explaining why it is public. `E2ELoginFunction.cs` is gated `#if E2E` + env-var. |

---

## 6. Positive signals matched

A conscious reward for what the program does well — per directive principle 12.

| Code | Evidence |
|---|---|
| `gha.POS-1` | Every workflow declares a top-level `permissions:` block with `contents: read` as the default (ci.yml:18, analyze-infra.yml:12, e2e.yml:16, stryker-nightly.yml:8, deploy-infra.yml:7, deploy-app.yml:7, deploy.yml:29, secrets-scan.yml:10). Write scopes escalated per workflow, not per org default. |
| `gha.POS-2` | `deploy-infra.yml:17-21` and `deploy-app.yml:78-82` use `azure/login@532459ea…` with `client-id`/`tenant-id`/`subscription-id` and **no** `client-secret`. `permissions: id-token: write` declared at workflow top. OIDC federation active for Functions deploy and Bicep deploy. |
| `gha.POS-3` | 27/27 `uses:` declarations pinned to 40-char commit SHAs across all 8 workflows. 100% pinning density. |
| `DSO-POS-9` (partial) | Secret scanning: Gitleaks (`secrets-scan.yml:22-28`) runs on every push to main + every PR to main + workflow_dispatch, with full-history `fetch-depth: 0`. Pre-commit hook (`scripts/pre-commit`) provides a second layer, opt-in only. |
| `DSO-POS-10` (partial) | Builds are repeatable from the same SHA because workflows pin to commits; NuGet is **not** yet reproducible (no lockfile — see `CICD-SEC-3`). Half the positive. |
| `bicep.POS-1` | Function App declares `identity: { type: 'SystemAssigned' }` (functions.bicep:82), plus five RBAC role assignments: Key Vault Secrets User, Cosmos DB Built-in Data Contributor, Storage Blob Data Owner, Storage Queue Data Contributor, Storage Table Data Contributor, Monitoring Metrics Publisher (functions.bicep:133-201). Zero shared-key / connection-string auth paths in the production identity graph. |
| `bicep.POS-2` | `functions.bicep:108-109` uses `@Microsoft.KeyVault(VaultName=…;SecretName=…)` references for `Blizzard__ClientId` and `Blizzard__ClientSecret`. No secret values in app settings. |
| `bicep.POS-3` | `keyvault.bicep:21-25` — `enableSoftDelete: true`, `enablePurgeProtection: true`, `softDeleteRetentionInDays: 90`, `enableRbacAuthorization: true`. `storage.bicep:41-43` — 7-day blob soft delete and container soft delete. |
| `bicep.POS-4` | Diagnostic settings on **every** scoped resource: Cosmos, Storage (blobServices), Key Vault, Functions, SWA. All target the same Log Analytics workspace with `dailyQuotaGb: 1` (loganalytics.bicep:17) — well under the 5 GB free grant per CLAUDE.md § Cost Guidance. |
| `bicep.POS-5` | `main.bicep:1-52` — every param has `@description`, string bound params have `@minLength`/`@maxLength`. No hardcoded names, regions, or domains in `main.bicep` or any module. All environment values come via workflow vars per `deploy-infra.yml:149-184`. |
| `dns.POS-1` | `api/Program.cs:99` `new CosmosClient(opts.Endpoint, new DefaultAzureCredential(), clientOptions)`. `api/Program.cs:174-177` Data Protection key ring wrapped with KV key via `DefaultAzureCredential`. `api/Services/KeyVaultSecretResolver.cs:14` uses the same pattern. Fall-back to `opts.AuthKey` path exists only for the Cosmos emulator (local / E2E). |
| `dns.POS-2` | `functions.bicep:108-109,114-115` — `Auth__KeyVaultUrl`, `Auth__DataProtectionKeyUri`, `Blizzard__ClientId`/`ClientSecret` all resolved via Key Vault references. |
| `dns.POS-3` | Two layers of security headers: `api/Middleware/SecurityHeadersMiddleware.cs:22-26` sets `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, `Strict-Transport-Security: max-age=31536000; includeSubDomains`, `Content-Security-Policy: default-src 'none'; frame-ancestors 'none'` on every API response. `staticwebapp.config.json:42-46` sets global headers on the SPA host including an explicit `Content-Security-Policy` allowlist (`script-src 'self' 'wasm-unsafe-eval'`, `connect-src` pinned to API + blob storage, `img-src` pinned to API + WoW render + blob storage) and `Permissions-Policy: camera=(), microphone=(), geolocation=()`. |
| `dns.POS-4` | `[RequireAuth]` attribute model enforced via `AuthPolicyMiddleware.cs`. Explicit short-circuit to 401 when session principal is absent — not merely "logged in", but a positive session check with cached reflection. |
| — (program-level positive) | `api/Program.cs:167-186` Data Protection ring persisted to Blob Storage and wrapped with Key Vault key — versionless URI so automatic key rotation works. Falls back to filesystem only when blob/KV URIs are not configured. Session encryption is correctly isolated from the Function App process. |
| — (program-level positive) | `ci.yml:107-118` and `deploy-app.yml:42-52` run `dotnet list lfm.sln package --vulnerable --include-transitive --format json` and `jq`-count the vulnerabilities; exit 1 on any. **Enforcing** dependency gate — a control whose removal would visibly change what ships. Compensates partially for the missing `packages.lock.json`. |
| — (program-level positive) | `E2ELoginFunction.cs` gated by `#if E2E` at compile time AND `E2E_TEST_MODE=true` env var at runtime. Defense in depth — even if the `#if` leaks, the endpoint returns 404 unless the env var is set. |
| — (program-level positive) | `deploy-infra.yml` executes `az deployment group what-if` (line 146) **before** `az deployment group create` (line 166). Preview-before-apply against production infra. |
| — (program-level positive) | Stryker.NET mutation testing nightly against `tests/Lfm.App.Core.Tests/` — provides a quality floor for test efficacy on the one project where WASM source generators don't block mutation. |

---

## 7. Supply-chain provenance check

Per `references/procedures/evidence-per-release.md`.

### Pinning density

| Dimension | Pinned | Total | Ratio |
|---|---|---|---|
| Workflow `uses:` → commit SHA | 27 | 27 | **1.00** |
| Dockerfile `FROM` → `@sha256:` | 0 | 2 | **0.00** |
| Compose `image:` → `@sha256:` | 0 | 4 | **0.00** |
| NuGet `PackageReference` → committed `packages.lock.json` | 0 | — | **no lockfile** |

- Workflows: `DSO-POS-3` — perfect.
- Containers: `DSO-HC-2` — MCR carve-out keeps this at `warn` not `block`.
- NuGet: `DSO-HC-2` — mitigated by enforcing `dotnet list package --vulnerable` gate in CI and deploy, but the reproducibility side of `DSO-POS-10` is not achieved.

### Dependency update cadence

`dependabot.yml` configures weekly updates for `nuget` (grouped `dotnet-minor`) and `github-actions`. Open-PRs limit 5 per ecosystem. `list_pull_requests state=open` returned `[]` — no pending Dependabot PRs at audit time; MCP view so no `DSO-LC-9` (no PR stale > 14 days).

### Release artifact inventory

`mcp__github__list_releases` returned `[]` — **no GitHub Releases published**. Every production deploy runs directly from a `push: main` event via `.github/workflows/deploy.yml`. There is no artifact intermediary between "source code on main" and "running in production."

This means for supply-chain attestation:

- **SBOM**: missing — not generated, not published, not queryable.
- **Signature**: missing — no cosign sign, no minisign, no in-toto.
- **SLSA provenance**: missing — no attestation.
- **Scan report per release**: partially present — Gitleaks and `dotnet list --vulnerable` run per deploy, but results are in ephemeral run logs, not attached to any release record.
- **CVE disclosure channel**: missing — no `SECURITY.md` → `DSO-HC-10`.

---

## 8. Evidence-per-release check

Most recent "release" in practice: the latest successful `Deploy` run on `main`, 2026-04-14 19:52 UTC (per MCP §10 Probe 2). Not an official Release, just a push-driven deploy.

For that deploy, the evidence-per-release table:

| §5.3 artifact | Present? | Where |
|---|---|---|
| SBOM (CycloneDX/SPDX) | No | — |
| Artifact signature (cosign / minisign / in-toto) | No | — |
| SLSA provenance attestation | No | — |
| Per-release scan report archived | Partial | Ephemeral run log only; no release-attached retention |
| Test results | Yes | `actions/upload-artifact@<sha>` → `dotnet-test-results` 14-day retention |
| CVE notes in release notes | N/A | No release notes |

Gap: **every one of the four high-confidence §5.3.1 / §5.3.6 / §5.3.7 / §5.3.16 positive signals is unachieved.** This is the single largest cluster of missing evidence in the audit.

Tightest path forward (one PR, under a day's work):

1. Add a `release` job to `deploy.yml` that runs after `deploy-app` succeeds on `push: main`. The job:
   - Generates a CycloneDX SBOM for `lfm.sln` via `CycloneDX/gh-dotnet-generate-sbom@<sha>` (or `dotnet-CycloneDX`).
   - Uses `sigstore/cosign-installer@<sha>` + `cosign sign-blob --yes` on the published artifacts with keyless OIDC signing.
   - Creates a dated `gh release` tag and attaches SBOM + signatures + cosign bundle.
   - Optionally, attaches a SLSA v1.0 provenance via `slsa-framework/slsa-github-generator@<sha>`.
2. Declare `SLSA Build Level 2` in a new `SECURITY.md` at repo root and link the release template.

Net effect: `DSO-HC-11` closes, `DSO-POS-1` + `DSO-POS-6` + `DSO-POS-16` + half of `DSO-POS-10` open.

---

## 9. Framework coupling report

Only frameworks relevant to a hobby WASM + Functions + Cosmos app are considered. Per CLAUDE.md's cost stance, NIS2 and CRA applicability are **not triggered** — a hobby project that does not process regulated personal data at NIS2 essential/important thresholds and is not a "product with digital elements" placed on the EU market in a commercial capacity falls outside both regimes. I still include one-line self-assessments so that if scope later changes, the gap is visible.

### SSDF (NIST SP 800-218) — practice-level coverage

| Practice | Status | Evidence / gap |
|---|---|---|
| `PO.1` Define security requirements | **missing** | No security requirements captured as stories or in `SECURITY.md`. `DSO-HC-0`. |
| `PO.3` Implement supporting toolchains | **partial** | Gitleaks + Dependabot + `dotnet list --vulnerable` + PSRule for Azure all configured; none flow to a persistent dashboard (Code Scanning disabled). |
| `PO.4` Define criteria + gather evidence | **missing** | No per-release evidence gathering — §8 gap. `DSO-SUB-7`. |
| `PO.5` Implement and maintain a secure environment | **partial** | OIDC federation, managed identities, Key Vault, pinned actions — all strong. CI/CD events not forwarded to SIEM. |
| `PS.1..PS.3` Protect software from tampering | **missing** | No artifact signing, no provenance, no release-time integrity check. `DSO-HC-11`. |
| `PW.1` Design with threat modeling | **missing** | No threat model document exists for any trust boundary. |
| `PW.4..PW.5` Reuse well-secured software | **partial** | Microsoft MCR images only; first-party frameworks (.NET, Azure Functions worker); no committed lockfile (`PW.4.1` partially). |
| `PW.7` Review and analyze code | **partial** | `dotnet format` + `TreatWarningsAsErrors=true`; no SAST (CodeQL / semgrep) wired. PSRule covers IaC only. |
| `PW.8` Test code | **enforcing** | Unit + component + E2E + Stryker mutation — strong ground-truth signal on App.Core. |
| `PW.9` Configure software to have secure settings by default | **enforcing** | WAF pillars applied per Bicep module. HTTPS-only, min TLS 1.2, http20, managed identity, RBAC, locks, soft-delete. |
| `RV.1` Identify and confirm vulnerabilities | **missing** | Dependabot alerts disabled. No intake channel (`DSO-HC-10`). |
| `RV.2` Assess, prioritize, and remediate | **missing** | No written SLA; no aging cohort dashboard. `DSO-HC-15`. |
| `RV.3` Analyze root causes | **partial** | `docs/security-reviews/` and `docs/quality-reviews/` contain historical reviews that sometimes produce rule/test additions (`DSO-POS-13` partial). |

### ASVS (recommended target: L1)

Self-assessment against ASVS 4.0.3 L1 chapters at a high level:

| Chapter | Status |
|---|---|
| V2 Authentication | partial — session cookie is Data-Protection-wrapped and KV-sealed; no negative auth tests documented |
| V3 Session Management | strong — versionless KV key, blob persistence, 90-day key lifetime |
| V4 Access Control | strong — `[RequireAuth]` middleware with short-circuit; BOLA risk at the per-object layer should be explicitly tested (E sub-lane S extension coverage in test-quality-audit) |
| V5 Validation, Sanitization, Encoding | unknown — not audited by this skill |
| V7 Error Handling and Logging | partial — structured logging present; `dns.HC-8` not fired because no `_logger.*.*.denied` pattern found, but absence of match is absence of evidence too |
| V9 Communications | strong — TLS 1.2+, HSTS set, HTTPS-only on resources |
| V10 Malicious Code | partial — no SAST wired |
| V13 API + Web Services | partial — no OpenAPI surface committed, so `dns.LC-1` shadow API check is best-effort |
| V14 Configuration | strong — security headers middleware + SWA globalHeaders |

### SCVS (recommended target: L1)

| Control family | Status |
|---|---|
| Inventory | **missing** — no SBOM |
| Structure | **partial** — Dependabot + weekly cadence; no lockfile |
| Provenance | **missing** — no provenance artifact |
| Integrity | **missing** — no signatures |
| Vulnerability response | **partial** — `dotnet list --vulnerable` gate is enforcing, but no SLA |
| Security testing | **missing** — no SCA dashboard ingest, just pass/fail |

---

## 10. Live-state block (MCP github)

```
Live state (MCP github) — tommimarkus/sisu-raidcal

Probe 1 — Branch protection on main:
  protected:          FALSE                      ← CICD-SEC-1 / DSO-HC-4 (block, live-verified)
  required reviews:   none
  required checks:    none
  CODEOWNERS:         absent
  admin bypass:       N/A (nothing to bypass)
  allow_force_pushes: default
  Verdict:            MISSING — not merely partial.

Probe 2 — Recent workflow runs (last 20, window ends 2026-04-14 19:52):
  pass rate:          19/20 = 95%
  failure:            E2E 2026-04-13 13:01 (workflow_dispatch, main) — no subsequent green E2E in window
  event mix:          push=13, schedule=2, workflow_dispatch=5
  pull_request:       0 runs in window
  pull_request_target: 0 runs (confirms static §4 CICD-SEC-4 finding)
  deploys on non-main: 0 runs
  Note: absence of pull_request event runs is consistent with a solo-developer
        direct-push pattern. With DSO-HC-4 unresolved, CI is bypassable by policy.

Probe 3 — Dependabot alerts:
  API state:          403 "Dependabot alerts are disabled for this repository"
  Update PRs:         configured in .github/dependabot.yml (nuget weekly, github-actions weekly)
  Interpretation:     vulnerability alerting is OFF at the repo level; update bot still runs.
                      DSO-HC-15 — no remediation SLA on findings, because no findings surface.

Probe 4 — Code scanning alerts:
  API state:          403 "Code scanning is not enabled for this repository"
  CodeQL default:     not enabled
  Uploaded SARIF:     only analyze-infra.yml PSRule SARIF as an artifact (no ingest)
  Interpretation:     PSRule results read by nobody between runs.

Probe 5 — Secret scanning alerts:
  API state:          403 "Resource not accessible by personal access token"
  Interpretation:     PAT-scope limitation; native GitHub secret scanning status
                      cannot be confirmed via this PAT. Gitleaks CI job compensates
                      with full-history scan on every push/PR.

Probe 6 — Collaborators:
  list_pull_requests: 0 open PRs at audit time
  Interpretation:     single-developer workflow; no stale collaborator detection
                      possible without org-level probe access.

Probe 7 — Releases:
  list_releases:      [] (empty)
  Interpretation:     §8 evidence-per-release degrades to "no release object
                      exists" — artifacts ship directly from a main push.
```

---

## 11. Presence-vs-efficacy verdict

**`partial`.**

The rubric's one-word classification is not "enforcing" because:

- `main` is unprotected in live state — and crucially, this is a **platform constraint** (GitHub Free private repos do not enforce rulesets), not a missing configuration. The mitigation is not "turn on branch protection" but "move every gate that currently runs in parallel workflows inline into the deploy path, and document the residual gap." Until §4 `CICD-SEC-1` compensating controls are applied, a gitleaks failure on a push to `main` is reported but does not stop the Deploy workflow from shipping to production.
- No artifact-side evidence exists per release: no SBOM, no signature, no provenance, no release object. A deploy is an event, not an auditable artifact.
- The cluster of surfacing-only controls (PSRule SARIF artifact-only, Dependabot alerts disabled, Code Scanning disabled) indicates a "scanning produces logs, nobody reads them" pattern. This is the `DSO-HC-12` + `DSO-HC-15` crossfire.

It is not "decorative" because:

- The build stage is genuinely enforcing. `dotnet list package --vulnerable --include-transitive` is an exit-1 gate. Gitleaks runs on every push and every PR with full-history fetch. `dotnet format --verify-no-changes` and `TreatWarningsAsErrors=true` block CI. These are controls whose removal would visibly change what ships the next time CI runs.
- The Azure posture is strong. Managed identity, Key Vault references for every secret, RBAC role assignments, `disableLocalAuth`, HTTPS-only, TLS 1.2 min, HTTP/2, diagnostic settings within the free grant, CanNotDelete locks. The Bicep modules implement the CLAUDE.md WAF checklist faithfully. A control like "Blizzard ClientSecret is a Key Vault reference" cannot be silently disabled without editing `infra/modules/functions.bicep` and redeploying; the removal would visibly change what runs.
- OIDC federation for the Functions and Bicep deploy paths. The only static-credential exception (`SWA_DEPLOYMENT_TOKEN`) is a documented platform limitation, not a design choice.
- Every workflow declares explicit minimum `permissions:`. Every third-party action is commit-SHA pinned. Every inputs interpolation uses the env-var sanitization pattern. These are `gha.POS-1` through `gha.POS-3` all live.

So: **partial — the runtime surface is well-secured; the supply-chain and governance surface is not.** The single highest-leverage change is branch protection; the second is release artifact generation. Together they close roughly 60% of the blocking findings.

---

## 12. Remediation worklist

Ordered by severity (block → warn → info), and within each severity by highest return per diff size. Each row is a specific action, not a directive.

| # | Severity | Finding | Action |
|---|---|---|---|
| 1 | warn | `DSO-HC-4` / `CICD-SEC-1` main unprotected — platform-constrained (GitHub Free private) | **Cannot** enable enforced branch protection on this plan. Apply compensating controls instead: (a) move `secrets-scan.yml` (gitleaks) and `analyze-infra.yml` (PSRule) under `workflow_call` and `needs:`-chain them into `deploy.yml` so a leak or IaC misconfig blocks deploy even though it lands on `main`; (b) enable signed commits (`commit.gpgsign=true`) + hardware-key MFA on the personal account; (c) add `scripts/pre-push` running `dotnet format --verify-no-changes && dotnet build && dotnet list package --vulnerable`; (d) add advisory `CODEOWNERS`; (e) document the accepted gap in `SECURITY.md` under "Known limitations". |
| 2 | block | `DSO-HC-10` no disclosure channel | Create `SECURITY.md` at repo root. Include: disclosure contact, triage SLA (e.g. "acknowledge within 72h, triage within 7d"), supported versions, security model notes, list of deliberately-public endpoints (`/api/battlenet/login`, `/api/battlenet/callback`, `/api/health/ready`, `/api/instances`, `/api/specializations`, `/api/privacy-contact`), cost stance declaration (`free` — no paid security scanners). Add `app/wwwroot/.well-known/security.txt` referencing `SECURITY.md`. |
| 3 | warn | `DSO-HC-0` no declared target | In the same `SECURITY.md`, declare: ASVS L1, SCVS L1, SLSA Build L1 (ceiling L2 after action #4). Cite that this is a hobby project with a free-tier cost stance. The declaration is itself the first evidence artifact. |
| 4 | warn | `DSO-HC-11` unsigned production artifacts + `DSO-HC-12` SARIF not ingested | Add a `release` job to `deploy.yml` (or a new `release.yml` triggered on `push: main` → success of `deploy-app`). Steps: `CycloneDX/gh-dotnet-generate-sbom@<sha>` → `sigstore/cosign-installer@<sha>` → `cosign sign-blob --yes --bundle` → `gh release create <date-sha>` attaching SBOM + signature bundle. Optional: `slsa-framework/slsa-github-generator@<sha>` for SLSA L2 provenance. Separately, change `analyze-infra.yml:30` to upload SARIF via `github/codeql-action/upload-sarif@<sha>` so findings land in Code Scanning with aging. |
| 5 | warn | `DSO-HC-15` Dependabot alerts disabled + Code Scanning disabled | Enable both in repo settings (both free). Dependabot alerts: **Settings → Code security → Dependabot alerts**. Code scanning: **Settings → Code security → Code scanning → Set up → Default**. Zero config change, zero cost, closes two findings. |
| 6 | warn | `DSO-HC-2` NuGet floating | Add `Directory.Build.props` at repo root with `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` under a `PropertyGroup`. Run `dotnet restore lfm.sln`. Commit every `packages.lock.json` that is produced. Add `--locked-mode` to CI restore (optional; strict). Repeatable builds at near-zero diff size. |
| 7 | warn | `docker.HC-2` Dockerfile + compose unpinned MCR | Pin `FROM mcr.microsoft.com/dotnet/sdk:10.0` and `FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated10.0` to `@sha256:<digest>` (capture via `docker manifest inspect`). Same for `docker-compose.local.yml:3,20` and `docker-compose.test.yml:3,20`. Under the MCR carve-out this is `warn` not `block`, but Dependabot can keep the digests fresh once `dependabot.yml` adds `package-ecosystem: docker`. |
| 8 | warn | `docker.HC-1` no `USER` directive | Add an explicit `USER 1000` (or equivalent) at the end of the `api/Dockerfile` runtime stage. The `azure-functions/dotnet-isolated` base image may already set this; pinning is defensive. |
| 9 | warn | `DSO-HC-7` SWA token exception | Document `SWA_DEPLOYMENT_TOKEN` in `SECURITY.md` as a known exception due to the SWA deploy action not supporting OIDC. Set a rotation cadence (quarterly). Track the Microsoft product feature for OIDC on SWA deploy; close the exception when available. |
| 10 | warn | `scripts/pre-commit` opt-in | Accept as opt-in (explicitly document in CLAUDE.md). The gitleaks CI job provides the authoritative enforcement layer. No diff. |
| 11 | info | `DSO-SUB-8` mild: `e2e.yml` path filter | With branch protection action #1 in place, CI runs on `pull_request: main` and the path-filter is an acceptable ergonomic optimization. Without branch protection it is an advisory gate that a direct push to `main` skips. Action #1 subsumes this. |
| 12 | info | HSTS not set in SWA `globalHeaders` | Add `"Strict-Transport-Security": "max-age=31536000; includeSubDomains; preload"` to `staticwebapp.config.json:42-46`. Free defense-in-depth. |
| 13 | info | No OpenAPI surface committed | Add an OpenAPI YAML covering the documented public and authenticated routes. Enables `dns.LC-1` shadow-API detection in future audits and supports BOPLA (`dns.LC-2`) static comparison. |
| 14 | info | No threat model per trust boundary | Draft a one-page STRIDE / data-flow diagram for: Battle.net OAuth callback; session cookie → API; Cosmos partition-key authorization; KV data-protection key wrap. Commit to `docs/threat-models/`. Update in the same PR as any architectural change (`DSO-POS-12`). |

---

## Footer

```
Extensions loaded: github-actions, bicep, dockerfile, dotnet-security
Cost stance:       free (source: skills/devsecops-audit/config.yaml)
Band 2 status:     free → Band 2 smells suppressed for bicep: B2-1..B2-6
MCP github:        available (user: tommimarkus, token: PAT)
MCP probe skips:   secret-scanning (403, PAT scope limitation)
Rubric:            docs/security-reference/devsecops.md
Honest limits (per rubric §8):
  - Whether a declared control is actually enforced in CI runs beyond the MCP-accessible window.
  - Whether a reported vulnerability is reachable from the codebase.
  - Whether a secret was used before it was rotated.
  - Whether a maintainer account is legitimately controlled.
  - Whether SBOM-declared dependencies are what actually got linked.
  - Whether runtime anomalies have occurred.
  - Whether a third-party supplier is currently compromised.
```
