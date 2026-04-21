#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude Code Stop hook — nudge the devsecops-audit skill.
#
# When Claude is about to finish a turn, check whether any security-relevant
# files in the repo have changed relative to main. If so, and the audit has
# not yet been prompted in this session, block the stop with a directive to
# invoke the devsecops-audit skill in quick mode.
#
# Sibling of stop-test-audit.sh. Uses the same session-marker loop-protection
# pattern — fires at most once per session per hook.
#
# Input: Stop hook JSON on stdin (session_id, cwd, transcript_path, ...).
# Output: {decision: "block", reason: ...} on stdout if any devsecops-relevant
#         file changed and not yet prompted this session; empty exit 0 otherwise.
#
# Wired up in .claude/settings.json under hooks.Stop.

set -euo pipefail

# ---- parse stdin ------------------------------------------------------------
input=$(cat)
session_id=$(jq -r '.session_id // empty' <<<"$input")
cwd=$(jq -r '.cwd // empty' <<<"$input")

# ---- locate repo root -------------------------------------------------------
# Use lfm.sln as the identity marker rather than basename, so the hook still
# fires inside worktrees under .worktrees/<slug>/ (CLAUDE.md mandates them).
[[ -z "$cwd" ]] && cwd="$PWD"
repo_root=$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null || true)
[[ -z "$repo_root" ]] && exit 0
[[ -f "$repo_root/lfm.sln" ]] || exit 0

# ---- loop protection: per-session marker ------------------------------------
marker_dir="$repo_root/.cache/claude-hooks"
marker="$marker_dir/devsecops-audit-prompted-${session_id:-unknown}"
[[ -f "$marker" ]] && exit 0

# ---- require main to exist --------------------------------------------------
git -C "$repo_root" rev-parse --verify --quiet main >/dev/null || exit 0

# ---- detect changed devsecops-relevant files --------------------------------
# Diff working tree + committed changes on branch against main, restricted to
# paths covered by the devsecops-audit skill's four extensions and its
# file-agnostic core signals:
#
#   github-actions  — .github/workflows/
#   bicep           — infra/
#   dockerfile      — Dockerfile*, docker-compose*.y?ml at repo root
#   dotnet-security — api/Functions/, api/Program.cs, app/Program.cs
#   core            — SECURITY.md, CODEOWNERS, .github/dependabot.yml
#
# Pathspecs are directories or explicit files; git's pathspec matcher treats
# directory args as prefix matches, which catches anything added under them.
changed=$(git -C "$repo_root" diff --name-only main -- \
  '.github/workflows/' \
  '.github/dependabot.yml' \
  'infra/' \
  'api/Functions/' \
  'api/Program.cs' \
  'app/Program.cs' \
  'SECURITY.md' \
  'CODEOWNERS' \
  'docker-compose.local.yml' \
  'Dockerfile' \
  2>/dev/null || true)

# Also include any file matching a Dockerfile.* pattern (rare but possible).
extra=$(git -C "$repo_root" diff --name-only main 2>/dev/null \
  | grep -E '(^|/)Dockerfile(\..+)?$|(^|/)docker-compose[^/]*\.y[a]?ml$' || true)

changed=$(printf '%s\n%s\n' "$changed" "$extra" | sed '/^$/d' | sort -u)

[[ -z "$changed" ]] && exit 0

# ---- emit block -------------------------------------------------------------
mkdir -p "$marker_dir"
touch "$marker"

jq -n --arg files "$changed" '{
  decision: "block",
  reason: (
    "Security-relevant files changed in this task:\n\n" + $files + "\n\n" +
    "Before finishing, invoke the `devsecops-audit` skill in quick mode on these files. " +
    "Read skills/devsecops-audit/SKILL.md, load any matching extensions (github-actions, bicep, " +
    "dockerfile, dotnet-security) based on the changed paths, resolve cost stance from " +
    "skills/devsecops-audit/config.yaml, and present per-finding output using the rubric fields " +
    "severity, stage, evidence, action, and rubric pointer. Cite smells by code — DSO-HC-*, gha.*, " +
    "bicep.*, docker.*, dns.*, CICD-SEC-* — never by prose. " +
    "This hook fires once per session — you will not be prompted again after this run."
  )
}'
