---
name: devsecops-audit
description: Use when auditing DevSecOps pipeline and application security posture — workflows, IaC, release artifacts, and code-level security smells — against docs/security-reference/devsecops.md. Supports quick single-target audits and deep whole-repo audits with conditional MCP live-state probes and configurable cost stance.
---

# DevSecOps Audit

## Overview

Audit a repository's DevSecOps posture against `docs/security-reference/devsecops.md`. The central question, from §1 of the rubric:

> If any control under audit were silently disabled for a sprint, would anyone notice — via a failed build, a blocked merge, a missed deploy, an alert someone reads, or a report someone acts on?

If no, the control is decorative. The skill's job is to distinguish enforcing controls from decorative ones and produce actionable findings.

**The rubric is `docs/security-reference/devsecops.md`.** This skill is the *workflow* for applying it. Findings cite rubric sections and smell codes by reference; the skill never duplicates rubric prose.

**Read `references/smell-catalog.md`** for the compact code list used in reports.

## Non-goals

- **General code quality** → use `code-quality-review`.
- **Test quality** → use `test-quality-audit`. The E2E sub-lane `S` complements this skill for CSP / CORS / cookie assertions.
- **Dead code / unused dependencies** → use `validate-hygiene`.
- **General Azure WAF review** → use `CLAUDE.md` § "Infrastructure Development" checklist directly.

## Audit modes

### Quick mode

**Use for:** a PR diff, a single workflow file, a single Bicep module, a single Dockerfile, a handful of changed C# files. Pre-merge feedback loop.

- Per-finding output only, no rollup.
- No MCP calls.
- Extensions load based on target type.

**Triggers:** "audit this PR", "check this workflow", "audit this Bicep", "is this Dockerfile OK", "devsecops quick".

### Deep mode

**Use for:** a whole-repo audit, pre-release posture check, quarterly review, large multi-file feature branch audit.

- Full twelve-section report per rubric §9.
- Stage coverage matrix, anti-pattern scan, smell matches, positive signals, evidence-per-release, MCP live-state probes, presence-vs-efficacy verdict, honest-limits statement.

**Triggers:** "full devsecops audit", "audit the pipeline", "security posture review", "devsecops deep".

**Default:** If the request is ambiguous, ask the user which mode they want.

## Extensions

The core workflow is framework-neutral. Extensions are per-stack smell packs loaded on demand:

| Extension | Applies to | Loaded when target matches |
|---|---|---|
| `extensions/github-actions.md` | CI workflow files | `.github/workflows/*.y?ml` |
| `extensions/bicep.md` | Azure IaC | `infra/**/*.bicep`, `**/*.bicepparam` |
| `extensions/dockerfile.md` | Containers | `**/Dockerfile`, `**/docker-compose*.y?ml` |
| `extensions/dotnet-security.md` | C# / .NET | `*.csproj`, `*.cs` under `api/` `app/` `shared/` `tests/`, `appsettings*.json` |

Multiple extensions may load for the same audit. Unknown target types proceed with only the **file-agnostic core smells**: `DSO-HC-0`, `DSO-HC-1`, `DSO-HC-10`, `DSO-HC-14`, `DSO-HC-15`. See `extensions/README.md` for adding a new extension.

Extensions never override core rules. They add namespaced smells or carve out false positives. Carve-outs win only for the exact pattern described.

## Cost stance

Some extensions (currently `bicep.md`) split smells into Band 1 (always-block) and Band 2 (cost-gated). The cost stance controls whether Band 2 fires. Three stances: `free`, `mixed`, `full`.

**Resolution precedence** (highest wins):

1. `--cost-stance=<value>` invocation arg
2. `config.yaml` in this skill dir
3. `CLAUDE.md` § "Cost Guidance" auto-detect
4. Hard default: `full`

See `references/procedures/cost-stance-detection.md` for the full resolver. Every report emits a one-line disclosure:

```
Cost stance: free (source: skills/devsecops-audit/config.yaml)
```

## Quick Mode Workflow

0. **Detect target type(s).** Glob the target. Possible types: workflow file, Bicep module, Dockerfile, `docker-compose*.yml`, C# source, `appsettings*.json`, `.csproj`, release manifest, `SECURITY.md`, `CODEOWNERS`. A single PR may hit multiple types.

0b. **Resolve cost stance.** Run `references/procedures/cost-stance-detection.md`. Record `{stance, source}` for the footer.

1. **Load matching extensions** per target type. Announce which extensions loaded.

2. **Read supporting context.** Base workflow templates, `CODEOWNERS`, branch-protection declarative config, `SECURITY.md` presence, `config.yaml`. Prevents flagging idiomatic helpers as smells.

3. **For each target file:**
   - Apply file-agnostic core smells first (`DSO-HC-0`, `DSO-HC-1`, `DSO-HC-10`, `DSO-HC-14`, `DSO-HC-15` where applicable).
   - Apply extension smells filtered by the extension's `Applies to:` metadata.
   - Apply extension carve-outs for idiomatic patterns.
   - For Band 2 smells: check the resolved cost stance; skip unless `full` or `mixed` with the specific code enabled.

4. **Emit findings** in the per-finding format (see Output below). No rollup, no stage matrix, no MCP probes.

## Deep Mode Workflow

0. **Detect & load.** Same as quick, but glob the whole repo: `.github/workflows/**`, `infra/**`, any `Dockerfile`, `docker-compose*.y?ml`, `api/**`, `app/**`, `shared/**`, `tests/**`, `SECURITY.md`, `CODEOWNERS`, `README.md`. Announce which extensions loaded.

0b. **Resolve cost stance** via the procedure. Record for the footer.

0c. **Probe MCP availability.** Attempt a single no-op `mcp__github__get_me` call. Success → live-state probes enabled. Any failure → MCP unavailable, record the skip, continue with static findings only. **Never retry.**

1. **Scope statement.** Which repos, workflows, environments, release artifacts are in scope. Anything explicitly excluded. Rubric §9 item 1.

2. **Declared target levels.** Does the repo declare ASVS / SCVS / SLSA / DSOMM / SAMM / NIS2 / CRA? If nothing is declared, emit `DSO-HC-0` as the first finding and continue. Rubric §9 item 2.

3. **Stage coverage matrix.** Run `references/procedures/stage-coverage-matrix.md`. Emit the eight-stage matrix with `enforcing` / `decorative` / `missing` classification per row. Rubric §9 item 3.

4. **Anti-pattern scan.** For each of `CICD-SEC-1..10`, scan the repo for the corresponding pattern. Emit one finding per match with file/line locator. Rubric §9 item 4.

5. **Smell matches.** Collect every `DSO-HC-*`, `DSO-LC-*`, and `DSO-SUB-*` finding produced so far, plus extension smells. Cite codes only; evidence pointers to file:line. Rubric §9 item 5.

6. **Positive signals.** Collect every `DSO-POS-*` and `<ext>.POS-*` matched. Rubric §9 item 6.

7. **Supply-chain provenance check.** Run `references/procedures/evidence-per-release.md`. Emit SBOM / signing / pinning / cadence block. Rubric §9 item 7.

8. **Evidence-per-release check.** For the most recent release, list which §5.3 artifacts exist and which are claimed in docs but missing from the release itself. Rubric §9 item 8.

9. **Framework coupling report.** For each of SSDF (PO / PS / PW / RV), CRA Annex I, NIS2 Article 21: which rows are demonstrably covered by repo evidence, which are claimed but not evidenced, which are missing. Rubric §9 item 9.

10. **MCP live-state probes** via `references/procedures/mcp-github-probes.md`, conditional on step 0c. Fetch branch protection, recent run exit codes, Dependabot / Code-scanning / Secret-scanning alerts, collaborators. Emit the `live-state` block.

11. **Presence-vs-efficacy verdict.** Single word: `enforcing` / `partial` / `decorative`. Backed by the finding list and the stage matrix. Rubric §9 item 10.

12. **Honest-limits statement.** Emit the §8 block verbatim listing what a static + MCP-read-only audit cannot determine. Do not invent certainty.

## Output format

### Per-finding shape (both modes)

```
[DSO-HC-2] workflow: .github/workflows/deploy.yml:14
  severity: block
  stage:    Build
  evidence: uses: actions/checkout@main   (floating tag)
  action:   Pin to commit SHA, e.g. actions/checkout@<40-char-sha>
  rubric:   devsecops.md §5.1 item 2; OpenSSF Scorecard Pinned-Dependencies
```

### Quick mode report

Findings block only. Footer:

```
Extensions loaded: github-actions, dotnet-security
Cost stance:       free (source: skills/devsecops-audit/config.yaml)
Rubric:            docs/security-reference/devsecops.md
```

### Deep mode report

All twelve sections. Remediation worklist ordered by severity (block → warn → info), then by stage. Footer adds:

```
MCP github:  available / unavailable
Cost stance: <stance> (source: <source>)
Rubric:      docs/security-reference/devsecops.md
Honest limits: <verbatim §8 block>
```

## Complementary skills

- `test-quality-audit` — E sub-lane S covers CSP / CORS / cookie assertions. Remediation actions for code-level security findings (e.g. `dns.HC-4`) include a text pointer: "Add an E2E test in the S sub-lane that asserts the fixed CORS config." Devsecops-audit never invokes test-quality-audit programmatically.
- `code-quality-review` — refactoring opportunities adjacent to a security finding belong there, not here.
- `validate-hygiene` — a pinned-but-unused dependency is dead code; a pinned-to-floating-tag dependency is a devsecops smell. Both skills may cite the same file from different angles.

## Honest limits

Per rubric §8, this skill cannot determine:

- Whether a declared control is actually enforced in CI runs beyond the MCP-accessible window.
- Whether a reported vulnerability is reachable from the codebase.
- Whether a secret was used before it was rotated.
- Whether a maintainer account is legitimately controlled.
- Whether SBOM-declared dependencies are what actually got linked.
- Whether runtime anomalies have occurred.
- Whether a third-party supplier is currently compromised.

Deep-mode reports include this list in the footer. Quick-mode reports omit it — the honest-limits statement is only meaningful alongside a whole-repo verdict.
