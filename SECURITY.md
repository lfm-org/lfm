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
| SLSA Build | L1 (declared) |

These targets reflect a hobby project with a `free` cost stance. `SECURITY.md` is the first evidence artifact for the SLSA Build L1 declaration.

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
- `GET /api/instances` — static reference data (WoW instances)
- `GET /api/reference/specializations` — static reference data (WoW specs)
- `GET /api/privacy-contact/email` — privacy contact email

All other endpoints require a valid session cookie enforced by `AuthPolicyMiddleware`.

## Cost Stance

`free` — see `CLAUDE.md` § Cost Guidance. Security tooling is limited to what fits within GitHub Free and Azure free tiers. Any control that requires a paid plan is documented under Known Limitations below.

## Known Limitations

### Pre-merge branch protection is not enforced

GitHub Free does not enforce branch protection rulesets on private repos. `main` is writable directly. Compensating controls:

1. **Inline security gates in the deploy workflow.** `secrets-scan.yml` (gitleaks) and `analyze-infra.yml` (PSRule) run as `workflow_call` jobs inside `deploy.yml`, gating `deploy-infra` and `deploy-app` via `needs:`. A secret leak or IaC misconfiguration on `main` blocks the deploy even though the commit has already landed.
2. **Signed commits.** Local `commit.gpgsign=true` with an SSH signing key. GitHub renders "Verified" badges for these commits.
3. **Opt-in pre-push hook.** `scripts/pre-push` runs format + build + vulnerability audit locally before a push reaches GitHub.
4. **Enforcing dependency audit.** `ci.yml` and `deploy-app.yml` both exit-1 on any vulnerable transitive NuGet package.
5. **TOTP MFA on the GitHub personal account.**

Upgrading the GitHub plan to enable enforced branch protection is not planned — the cost stance is `free`.

### Code Scanning / CodeQL is not available

GitHub Code Scanning requires GitHub Advanced Security, which is not included on the Free plan for private repos. The PSRule for Azure job runs and produces a SARIF artifact; `analyze-infra.yml` additionally writes a severity-count summary to `$GITHUB_STEP_SUMMARY` so a human reviewing the run page sees findings at a glance. The SARIF is not ingested into a Code Scanning dashboard.

### Dependabot alerts are not enabled

Deferred until the project stabilizes. Dependabot *update* PRs still run weekly (nuget, github-actions, and after Phase 1 also docker). Enable alerts later at **Settings → Code security → Dependabot alerts**.

### Hardware-key MFA is not in use

TOTP MFA is active. Hardware-key MFA is accepted as out of scope for a solo hobby project.

### `USER app` not set in `api/Dockerfile`

The Azure Functions `dotnet-isolated` prebuilt base image runs as the base image's default user and manages Functions-host privileges internally. Adding the .NET 8+ `USER app` non-root pattern would require `chown -R app:app /home` plus port 80 rebinding because non-root cannot bind privileged ports, and cannot be verified against production runtime behaviour without live deploys. Accepted — `docker.HC-1` is `warn` severity and the MCR carve-out further softens it. Re-evaluate if the project moves off the prebuilt Functions image.

## Known Exceptions

### `SWA_DEPLOYMENT_TOKEN` long-lived static token

`Azure/static-web-apps-deploy` does not yet support OIDC federation. The `SWA_DEPLOYMENT_TOKEN` GitHub secret is a long-lived static token kept for this reason alone. Every other Azure deploy path (Functions, Bicep) uses OIDC.

**Rotation cadence:** quarterly. Next rotation: track in project calendar.

**Retire when:** Microsoft ships OIDC support for the SWA deploy action.

### Dependabot auto-merge for low-risk bumps

`.github/workflows/dependabot-auto-merge.yml` enables GitHub's native auto-merge on Dependabot PRs matching a fixed policy: patch for every ecosystem, and minor for every ecosystem except docker. Major bumps and docker minor bumps stay manual.

**Trust boundary:** the `dependabot/fetch-metadata` action (SHA-pinned) and GitHub's Dependabot service for correct tag→SHA resolution. Compromise of either would allow a malicious low-risk-level version bump to land after passing CI.

**Compensating controls:** the `protect-main` ruleset's required status checks (CI, secrets-scan, analyze-infra) run the full build + unit/component tests + vulnerable-package audit + bundle-size budget before any auto-merge can complete. A failure in any required check keeps the PR open for manual review.

**Retire when:** never (permanent on a hobby-project cost stance).

## Supply-chain evidence (per release)

Every `push: main` that runs the deploy workflow produces a GitHub
Release with:

- CycloneDX SBOM (`sbom-api.cdx.json`)
- cosign keyless OIDC signature bundle per published blob
- SHA-256 manifest

Repository-level evidence always in place on `main`:

- Lockfile-enforced restore on CI (`RestoreLockedMode` gated on `$(CI)` in `Directory.Build.props`) — transitive package drift fails the CI build
- [`NuGet.config`](NuGet.config) — `<clear />` + `nuget.org` only, plus `<packageSourceMapping>` pinning every package ID to `nuget.org` (defense against dependency confusion)
- `CycloneDX` SBOM tool pinned via [`.config/dotnet-tools.json`](.config/dotnet-tools.json) — release job restores from the lockfile rather than installing an unpinned global tool
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

This is the evidence artifact for `SLSA Build L1`. SLSA L2 (provenance
attestation) is out of scope on cost grounds.

Per-file provenance is covered by SPDX headers and REUSE — supports the
OWASP SCVS L1 declaration in § Declared Security Targets.
