# Security Policy

## Reporting a Vulnerability

Deployers of this project each maintain their own security-contact policy.
For the security contact of a specific deployed instance, consult that
instance's `/.well-known/security.txt` (or the deployment's maintainer).

Forkers: set the `PRIVACY_EMAIL` environment variable (see [`example.env`](example.env))
and the build-time substitution in [`app/Lfm.App.csproj`](app/Lfm.App.csproj)
will insert it into the deployed `/.well-known/security.txt`.

For **vulnerabilities in the code of this repository itself** (not in a
specific deployment), open a GitHub issue tagged `security` on the
public repo, or — if the finding is sensitive — email the repository
maintainer listed in the GitHub repo profile.

**SLAs (for repository-level reports):**
- Acknowledge within 72 hours
- Triage within 7 days
- Remediation timeline communicated after triage

## Supported Versions

This is a rolling-`main` hobby project. There are no tagged versions; the latest `main` commit is always the supported state. Older commits are not supported.

## Declared Security Targets

| Framework | Target level |
|---|---|
| OWASP ASVS 4.0.3 | L1 |
| OWASP SCVS | L1 |

These targets reflect a hobby project with a `free` cost stance.

## Scope

### In scope
- `api/` — Azure Functions backend (.NET 10 isolated)
- `app/` — Blazor WASM frontend
- `shared/` — contracts
- `infra/` — Bicep templates and GitHub Actions workflows

### Out of scope
- Battle.net upstream OAuth provider (trust the supplier)
- Third-party NuGet / GitHub Action dependencies — report upstream instead
- Denial-of-service at the Azure platform level

## Public Endpoints

Endpoints that are intentionally unauthenticated and may be called by any client:

- `GET /api/battlenet/login` — initiates OAuth flow
- `GET /api/battlenet/callback` — receives OAuth callback
- `GET /api/health/ready` — liveness probe
- `GET /api/privacy-contact/email` — privacy contact email

All other endpoints require a valid session cookie enforced by `AuthPolicyMiddleware`.

## Cost Stance

`free` — see `CLAUDE.md` § Cost Guidance. Security tooling is limited to what fits within GitHub Free and Azure free tiers. Any control that requires a paid plan is documented under Known Limitations below.

## Known Limitations

### Pre-merge branch protection

The `protect-main` ruleset is active on `main` in the `lfm-org/lfm` public repo. Its current shape: pull request required for all changes, linear history required, rebase-only merge strategy, a set of required status checks that must pass, and organization admin bypass. For the authoritative current settings, see GitHub → Settings → Rules → `protect-main`.

Defense-in-depth layers on top of the ruleset:

1. **Production deploy entrypoint control.** Only `.github/workflows/deploy.yml` has a manual production deploy entry point. Leaf deploy workflows are reusable-only, and their deploy jobs require `github.ref == 'refs/heads/main'`.
2. **Protected production environment.** `deploy-infra.yml` and `deploy-app.yml` both bind deploy jobs to the `production` GitHub environment. Configure that environment to allow deployments only from `main`; keep required reviewers optional unless a second trusted reviewer can approve without blocking solo-maintainer recovery.
3. **Inline security gates in the deploy workflow.** `secrets-scan.yml` (gitleaks) and `analyze-infra.yml` (PSRule) run as `workflow_call` jobs inside `deploy.yml`, gating `deploy-infra` and `deploy-app` via `needs:`. A secret leak or IaC misconfiguration blocks the deploy even after a PR lands.
4. **Enforced monitoring deploy.** Metric alert deployment is part of `deploy-infra.yml` and must fail the deployment if alert resources cannot be deployed.
5. **Signed commits.** Local `commit.gpgsign=true` with an SSH signing key. GitHub renders "Verified" badges for these commits.
6. **Opt-in pre-push hook.** `scripts/pre-push` runs format + build + vulnerability audit locally before a push reaches GitHub.
7. **Enforcing dependency audit.** `ci.yml` and `deploy-app-build.yml` both exit-1 on any vulnerable transitive NuGet package before deploy.
8. **Lockfile-enforced restore on CI.** `RestoreLockedMode` via `$(CI)` in `Directory.Build.props` fails CI on any drift between `packages.lock.json` and the resolved graph.
9. **TOTP MFA on the GitHub personal account.**

### No CODEOWNERS file

No `CODEOWNERS` file is committed. Sole maintainer, so auto-review assignment adds no value; the `protect-main` ruleset already requires explicit PR approval. Re-evaluate and add pathed ownership when a second committer joins.

### CodeQL is public-repo only

CodeQL runs through `.github/workflows/codeql.yml` for code-affecting pull requests, code-affecting pushes to `main`, and manual dispatch. This is acceptable on the current cost stance because `lfm-org/lfm` is public. If the repository becomes private, revisit CodeQL before enabling any paid GitHub Code Security or GitHub Advanced Security feature.

Scheduled CodeQL scans are intentionally omitted to avoid extra runner usage on a hobby-project free-tier posture. The PSRule for Azure job still runs and produces a SARIF artifact; `analyze-infra.yml` additionally writes a severity-count summary to `$GITHUB_STEP_SUMMARY` so a human reviewing the run page sees findings at a glance.

### Dependabot alerts are not enabled

Deferred until the project stabilizes. Dependabot *update* PRs still run weekly (nuget, github-actions, and after Phase 1 also docker). Enable alerts later at **Settings → Code security → Dependabot alerts**.

### Hardware-key MFA is not in use

TOTP MFA is active. Hardware-key MFA is accepted as out of scope for a solo hobby project.

### `USER app` not set in `api/Dockerfile`

The Azure Functions `dotnet-isolated` prebuilt base image runs as the base image's default user and manages Functions-host privileges internally. Adding the .NET 8+ `USER app` non-root pattern would require `chown -R app:app /home` plus port 80 rebinding because non-root cannot bind privileged ports, and cannot be verified against production runtime behaviour without live deploys. Accepted — `docker.HC-1` is `warn` severity and the MCR carve-out further softens it. Re-evaluate if the project moves off the prebuilt Functions image.

## Known Exceptions

### `SWA_DEPLOYMENT_TOKEN` long-lived static token

`Azure/static-web-apps-deploy` does not yet support OIDC federation. The `SWA_DEPLOYMENT_TOKEN` GitHub secret is a long-lived static token kept for this reason alone. Store it as a `production` environment secret so it is released only after environment protection rules pass. Every other Azure deploy path (Functions, Bicep) uses OIDC.

**Rotation cadence:** quarterly. Next rotation: track in project calendar.

**Retire when:** Microsoft ships OIDC support for the SWA deploy action.

### Dependabot auto-merge for low-risk bumps

`.github/workflows/dependabot-auto-merge.yml` enables GitHub's native auto-merge on Dependabot PRs matching a fixed policy: patch for every ecosystem, and minor for every ecosystem except docker. Major bumps and docker minor bumps stay manual.

**Trust boundary:** the `dependabot/fetch-metadata` action (SHA-pinned) and GitHub's Dependabot service for correct tag→SHA resolution. Compromise of either would allow a malicious low-risk-level version bump to land after passing CI.

**Compensating controls:** the `protect-main` ruleset's required status checks (CI, secrets-scan, analyze-infra) run the full build + unit/component tests + vulnerable-package audit + bundle-size budget before any auto-merge can complete. A failure in any required check keeps the PR open for manual review.

**Retire when:** never (permanent on a hobby-project cost stance).

## Supply-chain evidence

This project does not publish release artifacts outside GitHub Actions. It is intended to be cloned or forked and deployed via the consumer's own CI/CD. Supply-chain evidence is therefore repository-level, always in place on `main`; production deploy builds also produce one-day internal deploy artifacts:

- Lockfile-enforced restore on CI (`RestoreLockedMode` gated on `$(CI)` in [`Directory.Build.props`](Directory.Build.props)) — transitive package drift fails the CI build. Enforced on 7 of 8 projects; the Blazor WASM project ([`app/Lfm.App.csproj`](app/Lfm.App.csproj)) opts out because its SDK-bundled implicit refs (`Microsoft.NET.Sdk.WebAssembly.Pack`, `Microsoft.DotNet.HotReload.WebAssembly.Browser`) drift independently of the pinned SDK version, making locked-mode impractical there. SDK version itself is pinned via `rollForward: disable` in [`global.json`](global.json).
- [`NuGet.config`](NuGet.config) — `<clear />` + `nuget.org` only, plus `<packageSourceMapping>` pinning every package ID to `nuget.org` (defense against dependency confusion)
- `persist-credentials: false` on every `actions/checkout` step in every workflow — `GITHUB_TOKEN` is not left on disk for subsequent steps to reuse
- Gitleaks release asset integrity verified via SHA-256 checksum file before extraction
- `.github/workflows/codeql.yml` — CodeQL C# analysis for the public repository without paid Code Security features
- `.github/workflows/deploy-app-build.yml` — deploy build generates a CycloneDX SBOM, packages API/app publish outputs as tar files, and creates provenance plus SBOM attestations for those tar files using GitHub public-repo artifact attestations
- `.github/workflows/deploy-app.yml` — deploy verifies provenance and CycloneDX SBOM attestations before extracting API/app tar files and deploying to Azure
- [`LICENSE`](LICENSE) — AGPL-3.0-or-later, project license
- [`NOTICE`](NOTICE) — copyright and "how to apply" pointer
- [`REUSE.toml`](REUSE.toml) — collective license coverage for files
  that cannot carry SPDX headers
- `.github/workflows/license-compliance.yml` — REUSE CI gate enforcing
  per-file SPDX headers on every PR
- `.github/workflows/dep-license-check.yml` — dependency license
  enforcement against `.github/license-allowlist.txt`
- [`docs/security-reviews/2026-04-16-dep-license-audit.md`](docs/security-reviews/2026-04-16-dep-license-audit.md)
  — one-time baseline audit of all NuGet dependencies

Per-file provenance is covered by SPDX headers and REUSE — supports the
OWASP SCVS L1 declaration in § Declared Security Targets.
