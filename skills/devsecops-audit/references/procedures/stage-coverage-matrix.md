# Stage Coverage Matrix

Build the eight-stage SDLC coverage matrix required by deep-mode step 3 of the skill. The stages come from devsecops.md §3; this procedure says *how* to populate them.

Rubric: devsecops.md §3, §9 item 3.

## Output shape

One row per SDLC stage. Each row has:

- **Stage name** (Design, Develop, Build, Test, Release, Deploy, Operate, Respond)
- **Controls present** (list of control names observed in the repo)
- **Controls enforcing** (subset of present where the control's removal would change what ships)
- **Controls decorative** (subset of present where the control's removal would change nothing observable)
- **Controls missing** (expected-by-§3, not present)

The classification `enforcing` vs `decorative` applies §1 of the rubric — the central presence-vs-efficacy question.

## Stage signals

### Design
- **Present if:** threat model documents exist in `docs/` or `docs/superpowers/specs/`; abuse cases in PR descriptions; data-classification notes; ASVS / SCVS target level declared in any security doc.
- **Enforcing if:** threat model updated in the same PR as the architectural change it covers (`DSO-POS-12`).
- **Decorative if:** threat model older than a year with no diffs (`DSO-LC-2`).
- **Missing if:** no threat model or abuse cases anywhere in the repo.

### Develop
- **Present if:** pre-commit secret scanning (`scripts/pre-commit` or similar); branch protection declarative config (`.github/branch-protection.yml`, ruleset JSON); `CODEOWNERS`; signed commits (`.gitattributes` or repo policy).
- **Enforcing if:** pre-commit hook is installed by default (verify `scripts/pre-commit` exists and is executable); branch protection is visible in declarative config and MCP confirms it's active on the default branch.
- **Decorative if:** `CODEOWNERS` exists but PRs bypass it; pre-commit hook exists but is opt-in.
- **Missing if:** any of the above are absent.

### Build
- **Present if:** SAST / SCA / IaC scan jobs in `.github/workflows/`; `dependabot.yml` or `renovate.json`; SBOM generation step; signing step.
- **Enforcing if:** scan jobs have no `continue-on-error: true` (`DSO-HC-6`) and the severity threshold blocks merges.
- **Decorative if:** scan jobs run but findings are never addressed (check dashboard age); SBOM generated but archived, never queried.
- **Missing if:** no scans, or scans exist only for `main` branch (`DSO-LC-10` / `DSO-SUB-8`).

### Test
- **Present if:** DAST / authenticated API scan / fuzzing / IAST job exists; business-logic tests exist for authz-critical endpoints.
- **Enforcing if:** tests block merges; the repo's E2E suite contains tests in test-quality-audit's E sub-lane S family (codes `E-HC-S1`..`E-HC-S5`), especially `E-HC-S4` for CSP / CORS / frame-ancestors browser-side blocking.
- **Decorative if:** tests exist but run only on `main`, or are gated on a flag that's never set.
- **Missing if:** no security-class tests.

### Release
- **Present if:** release workflow produces signed artifacts + SBOM + provenance.
- **Enforcing if:** `cosign sign` / `cosign verify` visible in the pipeline; SLSA provenance attestation step; SBOM attached to release page.
- **Decorative if:** artifacts built but unsigned (`DSO-HC-11`); SBOM generated but not attached.
- **Missing if:** release workflow doesn't produce verifiable artifacts at all.

### Deploy
- **Present if:** admission control or deploy-side verification (cosign verify, policy-as-code); secrets injected from Key Vault; OIDC federation for cloud credentials.
- **Enforcing if:** deploy job uses OIDC with no `client-secret` (`gha.POS-2`); deploy reads secrets via Key Vault references, not from CI secrets (`bicep.POS-2`).
- **Decorative if:** OIDC set up but fall-back to static creds remains enabled.
- **Missing if:** static cloud credentials as CI secrets (`DSO-HC-7`).

### Operate
- **Present if:** Application Insights / Log Analytics receiving traces; diagnostic settings on all Azure resources; CVE intake from OSV / NVD / GHSA.
- **Enforcing if:** alerts trigger on anomalies; scrubbing PII at ingestion; diagnostic settings configured per CLAUDE.md § Infrastructure Development.
- **Decorative if:** logs forwarded but never queried; alerts configured but never fire.
- **Missing if:** no observability, or CI/CD events not forwarded to any aggregator (`DSO-HC-12`).

### Respond
- **Present if:** `SECURITY.md` exists; `.well-known/security.txt` exists; incident runbook in `docs/`; post-incident RCA files in `docs/quality-reviews/` or similar.
- **Enforcing if:** `SECURITY.md` lists a disclosure contact and triage SLA; RCA files link to test / rule / policy additions (`DSO-POS-13`).
- **Decorative if:** `SECURITY.md` exists but is a template with no contact info.
- **Missing if:** no disclosure channel (`DSO-HC-10`).

## Walk procedure

For each stage in order:

1. Run the matching grep / file-existence checks from the stage signals above.
2. For each control found, classify as `enforcing` / `decorative` / `missing` using the classification rules in the signals section.
3. Record the verdict plus evidence pointers (file:line or file path).
4. Emit one stage row.

## Gotchas

- Do **not** re-emit smell findings here — findings from the anti-pattern scan (step 4) and smell-match step (step 5) are collected separately. The stage matrix references finding codes; it does not restate them.
- The matrix is the framework for the presence-vs-efficacy verdict in step 11. A stage with all controls `decorative` contributes to a `decorative` program verdict; a stage with all controls `enforcing` contributes to `enforcing`.
- When MCP is unavailable, the `enforcing` classification for Develop (branch protection), Build (recent run exit codes), and Deploy (OIDC verification) may degrade to "static config exists, enforcement unverified" — cite this in the matrix row and in the footer disclosure.
