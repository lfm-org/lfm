# Extension — GitHub Actions

**Applies to:** `.github/workflows/*.yml`, `.github/workflows/*.yaml`, reusable workflow files, composite action `action.yml`.

**Detection signals:**

- Path matches `.github/workflows/*.y?ml`.
- Any YAML file with a top-level `jobs:` key and at least one job containing `runs-on:` or `uses:`.
- `action.yml` / `action.yaml` files with a `runs:` block and `using: composite` | `using: docker` | `using: node20`.

**Applies to rubric sections:** §4 (CICD-SEC-1, -2, -4, -5, -6, -8), §5.1 items 2, 3, 5, 7, 9, §5.3 items 2, 3, 4, §6.2 (Indirect PPE).

## Smell codes

### `gha.HC-1` — Missing `permissions:` block

**Pattern:** a workflow file (top-level) or individual job without a `permissions:` key. GitHub's default when unspecified is `contents: write` / broad token scopes.

**Detection:** a `jobs:` block where no child job declares `permissions:` and the top level has no `permissions:` either.

**Severity:** `block`

**Rubric:** devsecops.md §5.1.3; OpenSSF Scorecard Token-Permissions.

**Remediation action:**
> Add `permissions: contents: read` at the workflow top level. Override per-job with the minimum needed write scopes. Document write scopes inline with a comment explaining why.

### `gha.HC-2` — Floating tag in `uses:`

**Pattern:** `uses: <owner>/<repo>@<ref>` where `<ref>` is not a 40-character commit SHA. Examples: `@main`, `@v1`, `@v1.2.3`, `@latest`.

**Detection (two-pass, default ripgrep — avoids lookaround):**

1. Enumerate every `uses:` line: `rg -nE 'uses:\s*\S+@\S+' .github/workflows/`
2. For each match, extract the ref after `@` and check whether it is exactly 40 hex characters. Any ref that is not → `gha.HC-2` finding. Refs that are 40 hex → `gha.POS-3` positive.

(If the caller insists on a single-pass regex, `rg --pcre2 'uses:\s*[^#\n]+@(?!([a-f0-9]{40}))[^\s#]+'` works with PCRE2. The two-pass form is preferred because it also drives `gha.POS-3` without a second scan.)

**Severity:** `block` (third-party) / `warn` (first-party actions when manual ownership verification confirms the action is in the same GitHub org as the audited repo — see carve-out below)

**Rubric:** devsecops.md §5.1.2; CICD-SEC-3; OpenSSF Scorecard Pinned-Dependencies.

**Remediation action:**
> Pin to a commit SHA: `uses: owner/repo@<40-char-sha>  # v1.2.3`. The tag in the trailing comment is human-readable; the SHA is the security boundary.

### `gha.HC-3` — `pull_request_target` with untrusted ref checkout

**Pattern:** a workflow triggered by `pull_request_target` that runs `actions/checkout` with `ref: ${{ github.event.pull_request.head.sha }}` or similar. This is the canonical Direct-PPE vector.

**Detection (multi-line):**
```
on:\s*\n\s*pull_request_target  →  actions/checkout  →  ref:.*pull_request
```

**Severity:** `block`

**Rubric:** devsecops.md §5.1.5; CICD-SEC-4; Scorecard Dangerous-Workflow.

**Remediation action:**
> Remove `pull_request_target` or, if required for writing back to the PR, split into a second workflow that runs with no checkout of untrusted code. Never check out `head.sha` in a `pull_request_target` workflow that has secret access.

### `gha.HC-4` — Shared deploy/merge identity

**Pattern:** a single workflow that both merges (via `GITHUB_TOKEN` with `contents: write`) and deploys to production (via cloud credentials). A compromise of either side grants both.

**Detection:** a single workflow file where one job has `contents: write` and a later job (same workflow) authenticates to cloud (OIDC login, Azure/AWS/GCP credentials, Service Principal secret).

**Severity:** `warn`

**Rubric:** devsecops.md §5.1.9; CICD-SEC-2; CICD-SEC-5.

**Remediation action:**
> Separate deploy from merge. A merge job writes to `main`; a deploy job triggers on `push: main` with its own identity. The deploy identity must have no `contents: write` and the merge identity must have no cloud deploy permission.

### `gha.HC-5` — Security scan with `continue-on-error: true`

**Pattern:** a job step that runs a SAST / SCA / DAST / IaC scan and has `continue-on-error: true`, or a severity threshold above `critical` without a comment justifying the exemption.

**Detection (ripgrep):**
```
continue-on-error:\s*true
```
Then manually verify the matched step is a security scan (CodeQL, trivy, semgrep, snyk, checkov, tfsec, etc.).

**Severity:** `block`

**Rubric:** devsecops.md §5.1.6; §1 (decorative control definition).

**Remediation action:**
> Remove `continue-on-error: true` or change severity gate to fail the build on `high` and above. If the scan is too noisy, fix the scanner config, don't hide the findings.

### `gha.HC-6` — User-controlled input interpolated into `run:`

**Pattern:** a `run:` block that uses `${{ github.event.* }}` or `${{ inputs.* }}` directly inside shell, without intermediate env-var sanitization. Indirect script injection risk.

**Detection (ripgrep):**
```
run:.*\$\{\{\s*(github\.event|inputs)\.
```

**Severity:** `block`

**Rubric:** devsecops.md §5.2.12; CICD-SEC-4 (indirect PPE form).

**Remediation action:**
> Assign the expression to an environment variable first, then reference it from the shell: `env: INPUT: ${{ inputs.foo }}` / `run: echo "$INPUT"`. The environment variable is a boundary the shell parser cannot cross.

## Positive signals

### `gha.POS-1` — Explicit minimum permissions

**Pattern:** every job declares `permissions:` with `contents: read` as the default, and write scopes only where needed with a justifying comment.

**Rubric:** devsecops.md §5.3.4.

### `gha.POS-2` — OIDC federation for cloud credentials

**Pattern:** the workflow declares `permissions: id-token: write` and authenticates via `azure/login@<sha>` or equivalent with `client-id` / `tenant-id` / `subscription-id` but **no** `client-secret`.

**Rubric:** devsecops.md §5.3.2; CICD-SEC-5 defended.

### `gha.POS-3` — Commit-SHA pinning for third-party actions

**Pattern:** ratio of `uses: owner/repo@<40-char-sha>` to total `uses:` declarations ≥ 0.9 (allowing first-party actions to be a small minority).

**Rubric:** devsecops.md §5.3.3.

## Carve-outs

- **Do not flag `gha.HC-2` as `block` for first-party actions pinned to a version tag when the action is in the same GitHub org as the audited repo.** "Same org" is not detectable from static grep alone — determine it by parsing `uses: owner/repo@ref` and comparing `owner` against the repo's GitHub org (inferred from `git remote get-url origin` or passed as an audit parameter). When the check is inconclusive, treat the finding as `block` — the carve-out is a downgrade, not a suppression. Monorepos may configure a known first-party-org list in `config.yaml` in a future version; for v1, the downgrade requires explicit opt-in per audit run.
- **Do not flag `gha.HC-1` on reusable workflow files that declare `permissions:` at the caller site.** The permissions declaration propagates.
