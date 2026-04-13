# DevSecOps Smell Catalog

Compact lookup table for every finding code the skill emits. The rubric prose lives in `docs/security-reference/devsecops.md`; this file is a code-to-section-pointer index. Findings in audit reports cite codes from this catalog and never restate the rubric.

**Rules enforced by the skill:**

1. Cite codes, do not restate the rubric.
2. Extensions never override core rules — they add new codes (namespaced) or carve out false positives for idiomatic patterns.
3. One severity per finding: `block` | `warn` | `info`. Don't stack.

## Core codes

### DSO-HC-* — High-confidence smells (devsecops.md §5.1)

| Code | Rubric | Description (one line; see rubric for full) |
|---|---|---|
| `DSO-HC-0` | §9 item 2 | No declared target levels (ASVS / SCVS / SLSA / DSOMM / SAMM / NIS2 / CRA). **Invented code**; fires when deep-mode step 2 finds nothing declared. |
| `DSO-HC-1` | §5.1.1 | Secrets in source control |
| `DSO-HC-2` | §5.1.2 | Unpinned CI action / image / dependency |
| `DSO-HC-3` | §5.1.3 | GitHub Actions workflow without declared `permissions:` |
| `DSO-HC-4` | §5.1.4 | No branch protection on default branch |
| `DSO-HC-5` | §5.1.5 | `pull_request_target` checking out untrusted ref (Direct-PPE) |
| `DSO-HC-6` | §5.1.6 | Security scan with `continue-on-error: true` or suppressed severity |
| `DSO-HC-7` | §5.1.7 | Long-lived static cloud credentials as CI secrets |
| `DSO-HC-8` | §5.1.8 | Container image without `@sha256:` digest or from unknown registry |
| `DSO-HC-9` | §5.1.9 | Deploy workflow and merge workflow share identity/token |
| `DSO-HC-10` | §5.1.10 | No `SECURITY.md` and no `/.well-known/security.txt` |
| `DSO-HC-11` | §5.1.11 | Production artifacts unsigned |
| `DSO-HC-12` | §5.1.12 | CI logs / audit events not forwarded off the build platform |
| `DSO-HC-13` | §5.1.13 | Dockerfile `USER root` or no `USER` directive |
| `DSO-HC-14` | §5.1.14 | `TODO: security review` / `FIXME: vuln` older than 90 days |
| `DSO-HC-15` | §5.1.15 | Scanner runs but no written remediation SLA |
| `DSO-HC-16` | §5.1.16 | Mixed-content HTTP on auth-serving site |
| `DSO-HC-17` | §5.1.17 | Mandatory MFA not enforced on SCM org or cloud root account |
| `DSO-HC-18` | §5.1.18 | Kubernetes `PodSecurity` admission missing or `privileged` |
| `DSO-HC-19` | §5.1.19 | Hardcoded signing keys or long-lived GPG keys in repo / CI secrets |
| `DSO-HC-20` | §5.1.20 | `Access-Control-Allow-Origin: *` with `Access-Control-Allow-Credentials: true` |

### DSO-LC-* — Low-confidence smells (devsecops.md §5.2)

| Code | Rubric | Description |
|---|---|---|
| `DSO-LC-1` | §5.2.1 | High SAST noise, low triage |
| `DSO-LC-2` | §5.2.2 | Threat model doc > 1 year with no diffs |
| `DSO-LC-3` | §5.2.3 | Security champion role exists, no PR reviews attributable for 3+ months |
| `DSO-LC-4` | §5.2.4 | > 20 third-party GitHub Actions from unrelated publishers |
| `DSO-LC-5` | §5.2.5 | K8s manifests missing `runAsNonRoot` / `readOnlyRootFilesystem` / `allowPrivilegeEscalation: false` |
| `DSO-LC-6` | §5.2.6 | Same SBOM digest across consecutive releases of different source |
| `DSO-LC-7` | §5.2.7 | IAM policy with `"Resource": "*"` or `"Action": "*"` |
| `DSO-LC-8` | §5.2.8 | Fuzz targets exist, no crashes in 6+ months on unsafe component |
| `DSO-LC-9` | §5.2.9 | Dependency update bot configured, PRs stale for weeks |
| `DSO-LC-10` | §5.2.10 | Security tests only run on `main` |
| `DSO-LC-11` | §5.2.11 | Terraform state file stored in repo or on build agent |
| `DSO-LC-12` | §5.2.12 | Parameterized pipeline with user-controlled input in `run:` blocks |
| `DSO-LC-13` | §5.2.13 | Post-incident reviews that don't result in test / rule / policy additions |

### DSO-POS-* — Positive signals (devsecops.md §5.3)

| Code | Rubric | Description |
|---|---|---|
| `DSO-POS-1` | §5.3.1 | Artifact provenance verifiable end-to-end |
| `DSO-POS-2` | §5.3.2 | OIDC federation replacing static cloud credentials |
| `DSO-POS-3` | §5.3.3 | Pinned dependencies everywhere (`@sha256:`, commit SHAs, lockfiles) |
| `DSO-POS-4` | §5.3.4 | Every workflow job declares minimum `permissions:` |
| `DSO-POS-5` | §5.3.5 | `SECURITY.md` + security.txt + disclosure contact + triage SLA |
| `DSO-POS-6` | §5.3.6 | SBOM generated per build, attached to release, queryable |
| `DSO-POS-7` | §5.3.7 | Written vulnerability remediation SLA tracked with aging cohorts |
| `DSO-POS-8` | §5.3.8 | Branch protection via declarative repo rules with CODEOWNERS reviews |
| `DSO-POS-9` | §5.3.9 | Secrets scanned pre-commit AND on push |
| `DSO-POS-10` | §5.3.10 | Reproducible or hermetic builds |
| `DSO-POS-11` | §5.3.11 | CI/CD audit events forwarded to SIEM with anomaly detection |
| `DSO-POS-12` | §5.3.12 | Threat model updated in same PR as architectural change |
| `DSO-POS-13` | §5.3.13 | Post-incident RCA producing lint / SAST / policy rule |
| `DSO-POS-14` | §5.3.14 | Role-targeted security training |
| `DSO-POS-15` | §5.3.15 | Fakes / in-memory infra for external services in tests |
| `DSO-POS-16` | §5.3.16 | SLSA Build Level declared and verifiable |
| `DSO-POS-17` | §5.3.17 | ASVS / SCVS target level declared in repo security docs |

### DSO-SUB-* — Subtle failures (devsecops.md §6)

| Code | Rubric | Description |
|---|---|---|
| `DSO-SUB-1` | §6.1 | Hash-pinned but unverified dependency (source not reviewed) |
| `DSO-SUB-2` | §6.2 | Indirect Poisoned Pipeline Execution (imported template from weaker repo) |
| `DSO-SUB-3` | §6.3 | Shadow / zombie APIs (old versions still routable) |
| `DSO-SUB-4` | §6.4 | BOLA (API1:2023) masquerading as functional auth |
| `DSO-SUB-5` | §6.5 | Mass assignment / BOPLA (API3:2023) |
| `DSO-SUB-6` | §6.6 | Logs reach SIEM but omit security-relevant fields |
| `DSO-SUB-7` | §6.7 | Compliance mapping without evidence pointers |
| `DSO-SUB-8` | §6.8 | Control scoped to default branch only |
| `DSO-SUB-9` | §6.9 | XZ-Utils class — long-dwell trusted-maintainer attack |
| `DSO-SUB-10` | §6.10 | "Rules File Backdoor" on AI coding assistants |
| `DSO-SUB-11` | §6.11 | Build-reproducibility regressions |
| `DSO-SUB-12` | §6.12 | Vulnerability suppressed as "not exploitable" without reachability analysis |

### CICD-SEC-* — OWASP CI/CD Security Top 10 (devsecops.md §4)

Reused verbatim from OWASP. The skill cites these codes directly rather than re-namespacing them.

| Code | Rubric | Description |
|---|---|---|
| `CICD-SEC-1` | §4.1 | Insufficient Flow Control Mechanisms |
| `CICD-SEC-2` | §4.2 | Inadequate Identity and Access Management |
| `CICD-SEC-3` | §4.3 | Dependency Chain Abuse |
| `CICD-SEC-4` | §4.4 | Poisoned Pipeline Execution (Direct / Indirect) |
| `CICD-SEC-5` | §4.5 | Insufficient Pipeline-Based Access Controls |
| `CICD-SEC-6` | §4.6 | Insufficient Credential Hygiene |
| `CICD-SEC-7` | §4.7 | Insecure System Configuration |
| `CICD-SEC-8` | §4.8 | Ungoverned Usage of Third-Party Services |
| `CICD-SEC-9` | §4.9 | Improper Artifact Integrity Validation |
| `CICD-SEC-10` | §4.10 | Insufficient Logging and Visibility |

### CICD-SEC-* — detection hints for deep-mode step 4

Deep-mode step 4 of the skill scans the repo for each CICD-SEC anti-pattern. The extensions already cover most of these via their own codes; the table below maps each CICD-SEC-N to the primary extension code(s) that detect it plus any repo-observable signals the extensions do not cover. When an extension finding fires, cite both the extension code and the parent CICD-SEC-N.

| Anti-pattern | Primary detection | Additional signals |
|---|---|---|
| `CICD-SEC-1` | MCP branch-protection probe (`DSO-HC-4`) | `.github/branch-protection.yml` absent, `CODEOWNERS` absent, admin bypass enabled |
| `CICD-SEC-2` | MCP collaborator probe, `gha.HC-4` | Stale accounts via `mcp__github__get_team_members`, shared PATs as CI secrets |
| `CICD-SEC-3` | `gha.HC-2`, `docker.HC-2`, `DSO-HC-2` | `*.csproj` `<PackageReference>` without committed lockfile; `dependabot.yml` absent |
| `CICD-SEC-4` | `gha.HC-3`, `gha.HC-6` (Direct-PPE); `DSO-SUB-2` (Indirect-PPE) | Workflow imports a template from a weaker repo — check `uses:` against known org allow-list |
| `CICD-SEC-5` | `gha.HC-1`, `gha.POS-2` (OIDC) inverse | Any workflow job with `permissions: write-all` or unspecified permissions |
| `CICD-SEC-6` | `dns.HC-1`, `DSO-HC-1`, `DSO-HC-7`, `docker.HC-3` | Long-lived `AZURE_CREDENTIALS` JSON in CI secrets; `gha.POS-2` inverse |
| `CICD-SEC-7` | `docker.HC-5` (privileged), `bicep.HC-3` (local auth) | Public CI dashboards; outdated plugins — requires MCP or manual inspection |
| `CICD-SEC-8` | `gha.HC-2` (third-party tag pin), `DSO-LC-4` (> 20 unrelated publishers) | Marketplace Actions beyond an org allow-list; unvetted OAuth apps granted org scopes |
| `CICD-SEC-9` | `DSO-HC-11` (unsigned artifacts), `docker.HC-2` | No `cosign sign` / `cosign verify` in release workflow; no SLSA provenance |
| `CICD-SEC-10` | `DSO-HC-12` (no log forwarding), `DSO-SUB-6` (missing security-relevant fields) | No `Microsoft.Insights/diagnosticSettings` on Functions/App Service; no SIEM integration |

Each row is a cross-reference, not a separate finding. Deep-mode step 4 rolls up the `CICD-SEC-*` coverage based on the extension findings already collected in step 5, plus the "Additional signals" checks when an extension does not cover them.

## Extension codes

Extension codes are namespaced with the extension's short name. Full descriptions live in each extension file; this section is a quick index.

### `gha.*` — github-actions extension

See `../extensions/github-actions.md` for the full table.

| Code | Intent |
|---|---|
| `gha.HC-1` | Missing `permissions:` block on workflow or job |
| `gha.HC-2` | Floating tag in `uses:` (e.g. `@main`, `@v1`) |
| `gha.HC-3` | `pull_request_target` with untrusted ref checkout |
| `gha.HC-4` | Shared deploy/merge identity in a single workflow |
| `gha.HC-5` | Security scan with `continue-on-error: true` |
| `gha.HC-6` | User-controlled input interpolated into `run:` |
| `gha.POS-1` | Explicit minimum `permissions:` on every job |
| `gha.POS-2` | OIDC federation via `id-token: write` + `azure/login@<sha>` |
| `gha.POS-3` | Commit-SHA pinning for third-party actions |

### `bicep.*` — bicep extension

See `../extensions/bicep.md` for the full table.

**Band 1 (always-block):**

| Code | Intent |
|---|---|
| `bicep.HC-1` | Shared keys / connection strings instead of managed identity |
| `bicep.HC-2` | TLS < 1.2 |
| `bicep.HC-3` | Local auth enabled where disable-able |
| `bicep.HC-4` | FTP / basic auth on App Service resources |
| `bicep.HC-5` | Secrets in params instead of Key Vault references |
| `bicep.HC-6` | Missing `CanNotDelete` lock on stateful resources |
| `bicep.HC-7` | Soft delete / purge protection disabled |
| `bicep.HC-8` | Diagnostic settings missing |
| `bicep.HC-9` | Hardcoded names / regions / domains |
| `bicep.HC-10` | `http20Enabled: false` or client affinity on stateless APIs |
| `bicep.HC-11` | Param missing `@description` / `@minLength` / `@maxLength` |

**Band 2 (cost-gated):**

| Code | Intent |
|---|---|
| `bicep.B2-1` | Defender for Cloud Standard tier absent |
| `bicep.B2-2` | Private Link / private endpoints absent |
| `bicep.B2-3` | Multi-region active-active absent |
| `bicep.B2-4` | WAF on App Gateway / Front Door Premium absent |
| `bicep.B2-5` | HSM-backed / Premium Key Vault with CMK absent |
| `bicep.B2-6` | Azure DDoS Protection Standard absent |

**Positive signals:**

| Code | Intent |
|---|---|
| `bicep.POS-1` | Managed identity usage |
| `bicep.POS-2` | Key Vault references in app settings |
| `bicep.POS-3` | Soft delete / purge protection enabled |
| `bicep.POS-4` | Diagnostic settings within free grant |
| `bicep.POS-5` | Fully parameterized modules |

### `docker.*` — dockerfile extension

See `../extensions/dockerfile.md` for the full table.

| Code | Intent |
|---|---|
| `docker.HC-1` | `USER root` or no `USER` directive |
| `docker.HC-2` | `FROM image:tag` without `@sha256:` digest |
| `docker.HC-3` | Build args containing secret material |
| `docker.HC-4` | Writable root filesystem (missing `read_only: true` in compose) |
| `docker.HC-5` | Privileged container (`privileged: true`) |
| `docker.POS-1` | All `FROM` references pinned to `@sha256:` |
| `docker.POS-2` | Non-root `USER` declared |

### `dns.*` — dotnet-security extension

See `../extensions/dotnet-security.md` for the full table.

| Code | Intent |
|---|---|
| `dns.HC-1` | Secret material in `appsettings*.json` / `.csproj` / `*.props` |
| `dns.HC-2` | `[AllowAnonymous]` on a non-public endpoint |
| `dns.HC-3` | Missing `[Authorize]` / `AuthorizationLevel.Function` on non-public endpoint |
| `dns.HC-4` | `AllowAnyOrigin()` combined with `AllowCredentials()` |
| `dns.HC-5` | Missing security headers middleware (CSP / HSTS / XFO / Referrer-Policy) |
| `dns.HC-6` | Connection string constructed via string concatenation |
| `dns.HC-7` | Cosmos / Blob / Service Bus client built with shared key where MI is feasible |
| `dns.HC-8` | Authz denial log missing actor / resource / decision / trace-id |
| `dns.LC-1` | Shadow / zombie function endpoint (registered, not in route inventory) |
| `dns.LC-2` | Mass assignment / BOPLA risk (deserialization into undocumented fields) |
| `dns.POS-1` | Managed identity client construction |
| `dns.POS-2` | Key Vault reference resolution |
| `dns.POS-3` | Explicit CSP / security headers middleware |
| `dns.POS-4` | `[Authorize(Roles=...)]` with specific role checks |
| `dns.POS-5` | OpenAPI-driven request models validated against schema |
