# DevSecOps Audit Skill — Smoke Test

**Date:** 2026-04-13
**Branch:** claude/devsecops-audit-skill

Static smoke test of the `devsecops-audit` skill patterns against the lfm repo. This validates that extension grep patterns compile and produce findings against real repo content. End-to-end agent invocation (`Agent({ subagent_type: 'devsecops-audit' })`) is deferred — the skill is auto-discoverable in the harness once the commit lands and the system reminder lists it.

## Extension pattern coverage

### github-actions

| Probe | Pattern | Result |
|---|---|---|
| `gha.HC-2` floating tag | `uses:\s*[^#]+@(main\|master\|latest\|v[0-9]+)\b` | **0 matches** — all third-party action uses are SHA-pinned |
| `gha.HC-1` permissions present | `^permissions:` at workflow top level | 8 / 8 workflow files declare `permissions:` (100% compliance) |

**Verdict:** repo is clean for the two high-confidence github-actions smells. `gha.POS-1` (explicit minimum permissions) and `gha.POS-3` (SHA-pinned actions) would both fire as positive signals.

### bicep

| Probe | Pattern | Result |
|---|---|---|
| `bicep.HC-1` shared keys / listKeys | `listKeys\(\|primaryKey\s*:\|accountKey\s*:` | 0 matches |
| `bicep.HC-3` local auth enabled | `disableLocalAuth\s*:\s*false\|allowSharedKeyAccess\s*:\s*true` | 0 matches |
| `bicep.HC-7` soft delete / purge protection disabled | `enableSoftDelete\s*:\s*false\|enablePurgeProtection\s*:\s*false` | 0 matches |
| `bicep.POS-1` managed identity declared | `type:\s*'SystemAssigned'\|type:\s*'UserAssigned'` | 1 match |

**Verdict:** no Band 1 bicep smells fire on the current infra; `bicep.POS-1` fires once. Band 2 smells are suppressed under `costStance: free`, as expected.

### dockerfile

| Probe | Pattern | Result |
|---|---|---|
| `docker.HC-2` unpinned compose image | portable variant: `^\s*image:\s*[^@[:space:]]+:[^@[:space:]]+$` | **2 matches** — `docker-compose.local.yml` and `docker-compose.test.yml` both pull `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview` with a floating tag and no `@sha256:` digest |
| `docker.POS-1` pinned sha256 | `@sha256:[a-f0-9]{64}` | 0 matches |

**Verdict:** real `docker.HC-2` findings on both compose files. Under the `docker.HC-2` carve-out for `mcr.microsoft.com` registries the severity downgrades from `block` to `warn`. Still a smell; should be addressed when the emulator image ships with pinnable digests.

**Pattern portability note:** the extension's reference regex uses `[^@\n]` for line-end exclusion, which is not uniformly supported across grep dialects. The portable form `[^@[:space:]]` produces identical results and is recommended when an agent synthesizes a grep from the extension.

### dotnet-security

| Probe | Pattern | Result |
|---|---|---|
| `dns.HC-4` broken CORS | `AllowAnyOrigin\(\)` | 0 matches |
| `dns.HC-2` AllowAnonymous | `\[AllowAnonymous\]` | 0 matches |
| `dns.HC-7` Cosmos ConnString shared key | `new\s+CosmosClient\s*\(.*ConnectionString` | 0 matches |
| `dns.POS-1` DefaultAzureCredential | `DefaultAzureCredential\|ManagedIdentityCredential` | **4 matches** |

**Verdict:** strong positive signal for managed identity usage. Zero Band 1 `dns.*` findings on the current API and app code.

## Cost stance resolution

- `skills/devsecops-audit/config.yaml` → `costStance: free`
- `CLAUDE.md` § "Cost Guidance" auto-detect matches on: *"Hobby project. Prefer free tiers..."* (line 14)
- Precedence: `config.yaml` wins over `CLAUDE.md` auto-detect; both independently resolve to `free`. If `config.yaml` were deleted, the auto-detect fallback would still reach `free`.
- Effective stance: **`free`**. Band 2 `bicep.B2-*` smells suppressed with one `info` line per extension.

## Discovery

- `.claude/skills` is a symlink to `../skills` → `skills/devsecops-audit/SKILL.md` is visible to the harness.
- `.claude/agents` is a symlink to `../agents` → `agents/devsecops-audit.md` is visible.
- After the commit for the SKILL.md and agent wrapper landed, the session's `<system-reminder>` skill list now contains `devsecops-audit`. Auto-discovery via symlink confirmed.

## Manual invocation test (deferred)

End-to-end dispatch via `Agent({ subagent_type: 'devsecops-audit' })` requires a live session. This smoke test validates the static components only:

- File structure ✓
- Extension regex intent ✓ (with the `[:space:]` portability note above)
- Cost-stance resolver ✓
- Discovery ✓

A follow-up manual run should invoke the agent in quick mode on a PR diff and in deep mode on the whole repo to verify the report format and the twelve-section deep-mode rollup. That is explicitly out of scope for this smoke test per the plan's "Manual invocation test (deferred)" note.

## Findings summary (what a real quick-mode audit would emit today)

Running the skill in quick mode against the whole repo today would produce:

- `docker.HC-2` (severity `warn`, carve-out applied): 2 findings — cosmos emulator image unpinned in `docker-compose.local.yml:3` and `docker-compose.test.yml:3`.
- Positive signals: `gha.POS-1` (all 8 workflows), `gha.POS-3` (all third-party uses pinned), `dns.POS-1` (4 managed-identity client constructions), `bicep.POS-1` (1 managed identity declaration).
- Zero Band 1 smells from github-actions, bicep, or dotnet-security.
- Band 2 bicep smells suppressed with one `info` line (`costStance: free`).

This is the expected profile for a repo that already follows CLAUDE.md's "Mandatory Git Workflow" and "Infrastructure Development" pillars. The skill's value on this repo is quarterly / pre-release deep-mode audits that cover the stage coverage matrix and the evidence-per-release check, not pre-merge quick-mode hunting for clearly-absent smells.
