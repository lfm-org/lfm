#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude/Codex Stop hook — nudge the devsecops-audit skill.
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
# Wired up in .claude/settings.json and .codex/hooks.json under hooks.Stop.

set -euo pipefail

hook_name="devsecops-audit"

# ---- parse stdin ------------------------------------------------------------
input=$(cat)
session_id=$(jq -r '.session_id // empty' <<<"$input")
cwd=$(jq -r '.cwd // empty' <<<"$input")
stop_hook_active=$(jq -r '.stop_hook_active // false' <<<"$input")

# ---- locate repo root -------------------------------------------------------
# Use lfm.sln as the identity marker rather than basename, so the hook still
# fires inside worktrees under .worktrees/<slug>/ (CLAUDE.md mandates them).
[[ -z "$cwd" ]] && cwd="$PWD"
repo_root=$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null || true)
[[ -z "$repo_root" ]] && exit 0
[[ -f "$repo_root/lfm.sln" ]] || exit 0

# ---- loop protection: per-session marker ------------------------------------
marker_dir="$repo_root/.cache/agent-hooks"
marker="$marker_dir/devsecops-audit-prompted-${session_id:-unknown}"

debug_log() {
  [[ "${AGENT_HOOK_DEBUG:-}" == "1" ||
     "${CODEX_HOOK_DEBUG:-}" == "1" ||
     "${CLAUDE_HOOK_DEBUG:-}" == "1" ]] || return 0

  mkdir -p "$marker_dir" || return 0
  {
    jq -cn \
      --arg hook "$hook_name" \
      --arg event "$1" \
      --arg session_id "${session_id:-unknown}" \
      --arg cwd "$cwd" \
      --arg repo_root "$repo_root" \
      --arg changed "${changed:-}" \
      '{ts: now | todateiso8601, hook: $hook, event: $event, session_id: $session_id, cwd: $cwd, repo_root: $repo_root, changed: $changed}' \
      >>"$marker_dir/debug.jsonl"
  } 2>/dev/null || true
}

if [[ "$stop_hook_active" == "true" ]]; then
  debug_log "skip-stop-hook-active"
  exit 0
fi

if [[ -f "$marker" ]]; then
  debug_log "skip-marker-exists"
  exit 0
fi

# ---- require main to exist --------------------------------------------------
if ! git -C "$repo_root" rev-parse --verify --quiet main >/dev/null; then
  debug_log "skip-no-main"
  exit 0
fi

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

if [[ -z "$changed" ]]; then
  debug_log "skip-no-changes"
  exit 0
fi

# ---- emit block -------------------------------------------------------------
mkdir -p "$marker_dir"
touch "$marker"
debug_log "emit-block"

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
