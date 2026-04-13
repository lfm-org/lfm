# Evidence Per Release

Deep-mode step 7 and step 8 of the skill. Answers: for the most recent release, what artifacts exist that would let an external auditor verify the claimed security levels?

Rubric: devsecops.md §2.3, §5.3.1, §5.3.6, §9 items 7 and 8.

## Inputs

- The repo (static).
- `mcp__github__list_releases` (if MCP is available) for release metadata.
- `mcp__github__get_latest_release` for artifact list.

## Core question

For every release the audit considers, does it produce:

1. **SBOM** in a machine-readable format (CycloneDX, SPDX)?
2. **Signature** on the artifacts (cosign, minisign, in-toto)?
3. **SLSA provenance** attestation?
4. **Scan report** (SAST, SCA, container scan) associated with the release?
5. **CVE disclosure** in release notes when applicable?

Absence of any is a gap. Claiming any in docs without the actual artifact is `DSO-SUB-7` — compliance mapping without evidence pointers.

## Walk procedure

### Step 1 — Enumerate recent releases

If MCP is available:
```
mcp__github__list_releases (per_page=5)
```
Pick the most recent published release. If the repo has no releases, skip this procedure and emit: "Repo has no published releases — evidence-per-release check skipped. Recommend cutting a release through the deploy workflow to begin accumulating evidence."

If MCP is unavailable: enumerate release tags from `git -C <repo> tag --sort=-creatordate | head -5`. Use the most recent tag as the release point and note in the report that release asset details are unverifiable without MCP.

### Step 2 — Artifact inventory

For the selected release, list:

- Release assets (attached files). Tabulate name, size, content-type.
- Any `.sbom.json` / `.cdx.json` / `.spdx.json` file.
- Any `.sig` / `.pem` / `.cert` file (cosign artifacts).
- Any `.intoto.jsonl` or `.slsa.json` file (provenance).
- Release-notes text.

If MCP is unavailable: inspect committed `CHANGELOG.md` / release notes file for the selected tag, and inspect `artifacts/` or similar directories for committed artifacts. Note the degraded confidence.

### Step 3 — Cross-reference against claims

Read `SECURITY.md`, `README.md`, and `docs/` for any claims of:

- "SLSA Build Level N"
- "ASVS Level N"
- "Signed releases"
- "SBOM published"
- Any SSDF / NIS2 / CRA / SOC 2 / ISO 27001 claim.

For each claim, find the matching artifact in step 2. Any claim without a matching artifact is a `DSO-SUB-7` finding.

### Step 4 — Pinning density

Compute: (pinned dependencies / total dependencies) across:
- `.github/workflows/` `uses:` lines (SHA-pinned vs tag-pinned)
- `Dockerfile` `FROM` lines (digest-pinned vs tag-pinned)
- `*.csproj` `PackageReference` versions (lockfile present?)

Report the ratio. Under 0.9 → `DSO-HC-2` finding for each unpinned entry. Over 0.9 → `DSO-POS-3` positive signal.

### Step 5 — Dependency update cadence

Read `dependabot.yml` or `renovate.json` if present. Emit:

- Bot configured: yes / no
- Schedule: (daily / weekly / monthly)
- Open PRs from the bot: count via `mcp__github__list_pull_requests --state=open --label=dependencies` when MCP is available; otherwise skip and note.
- Age of oldest open bot PR: if > 14 days → `DSO-LC-9` finding.

### Step 6 — Emit the evidence-per-release block

Final block in the deep-mode report includes:

```
Most recent release: <tag>
  SBOM:        present (CycloneDX) / missing
  Signature:   present (cosign) / missing
  Provenance:  present (SLSA L1) / missing / claimed L2 not verified
  Scan report: present / missing / decorative (no remediation SLA)

Pinning density:
  Workflows: X/Y (ratio)
  Dockerfile: X/Y
  .csproj lockfile: present / missing

Dependency bot:
  Configured: yes / no
  Oldest open bot PR: N days (> 14 → DSO-LC-9)
```

## Gotchas

- MCP unavailability degrades confidence across all five steps but does not stop the procedure. Static evidence alone is still valuable; the report must disclose the MCP skip.
- A release with SBOM but no signature is not "half-secure" — an unsigned SBOM has no integrity guarantee. Report both cells honestly.
- Claims in `SECURITY.md` that cannot be matched to artifacts are `DSO-SUB-7` regardless of how strong they sound.
