#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude Stop hook — nudge the software-design review skill.
#
# When Claude is about to finish a turn, check whether any source code,
# project, or shell-script files have changed relative to main. If so, and
# the audit has not yet been prompted in this session, block the stop with
# a directive to invoke the souroldgeezer-design:software-design skill in
# review mode (quick) on the changed files.
#
# Sibling of stop-test-audit.sh, stop-devsecops-audit.sh, and
# stop-responsive-audit.sh. Uses the same session-marker loop-protection
# pattern — fires at most once per session.
#
# Input: Stop hook JSON on stdin (session_id, cwd, transcript_path, ...).
# Output: {decision: "block", reason: ...} on stdout if any source / project /
#         script file changed and not yet prompted this session;
#         empty exit 0 otherwise.
#
# Wired up in .claude/settings.json under hooks.Stop. Codex support deferred.

set -euo pipefail

hook_name="software-design-audit"

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
marker="$marker_dir/software-design-audit-prompted-${session_id:-unknown}"

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

# ---- detect changed source / project / script files -------------------------
# Diff working tree + committed changes on branch against main, restricted to
# files in scope of the software-design skill:
#
#   .NET source     — .cs files (production code; tests/ excluded — those are
#                     covered by test-quality-audit)
#   .NET projects   — *.csproj, *.sln, *.slnx, Directory.Build.{props,targets},
#                     global.json (boundary / dependency-direction signals)
#   shell scripts   — *.sh, *.bash, *.zsh (shell-script extension scope)
#
# Razor markup is intentionally not in scope here: visual / a11y / i18n
# concerns belong to responsive-audit. Razor code-behind (.razor.cs) is also
# excluded to keep the two hooks disjoint on the UI tree.
changed=$(git -C "$repo_root" diff --name-only main 2>/dev/null \
  | grep -vE '^tests/' \
  | grep -vE '\.razor\.cs$' \
  | grep -E '\.cs$|\.csproj$|\.sln[x]?$|(^|/)Directory\.Build\.(props|targets)$|(^|/)global\.json$|\.(sh|bash|zsh)$' \
  || true)

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
    "Source / project / shell-script files changed in this task:\n\n" + $files + "\n\n" +
    "Before finishing, invoke the `souroldgeezer-design:software-design` skill in review mode (quick) on these files. " +
    "Read the skill reference at `souroldgeezer-design/docs/software-reference/software-design.md`, apply the rubric, " +
    "load matching extensions (dotnet, shell-script) based on the changed paths, and present per-finding output using " +
    "the rubric fields (bucket, layer, severity, evidence, action, ref). Cite smells by code — SD-*, dotnet.SD-*, " +
    "shell.SD-* — and only emit findings that are actionable. " +
    "This hook fires once per session — you will not be prompted again after this run."
  )
}'
