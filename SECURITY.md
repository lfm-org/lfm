# Security Policy

## Reporting a Vulnerability

Email: lfmprivacy.catwalk113@passmail.net

**SLAs:**
- Acknowledge within 72 hours
- Triage within 7 days
- Remediation timeline communicated after triage

Please do not open public issues for security reports. Prefer an encrypted email if the finding is sensitive.

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
- `GET /api/specializations` — static reference data (WoW specs)
- `GET /api/privacy-contact` — privacy contact email

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

## Known Exceptions

### `SWA_DEPLOYMENT_TOKEN` long-lived static token

`Azure/static-web-apps-deploy` does not yet support OIDC federation. The `SWA_DEPLOYMENT_TOKEN` GitHub secret is a long-lived static token kept for this reason alone. Every other Azure deploy path (Functions, Bicep) uses OIDC.

**Rotation cadence:** quarterly. Next rotation: track in project calendar.

**Retire when:** Microsoft ships OIDC support for the SWA deploy action.

## Supply-chain evidence (per release)

After Phase 3 of this plan lands, every `push: main` that runs the deploy workflow produces a GitHub Release with:

- CycloneDX SBOM (`sbom-api.cdx.json`)
- cosign keyless OIDC signature bundle per published blob
- SHA-256 manifest

This is the evidence artifact for `SLSA Build L1`. SLSA L2 (provenance attestation) is out of scope on cost grounds — see remediation spec for rationale.
