#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude/Codex Stop hook — nudge the responsive-design review skill.
#
# When Claude is about to finish a turn, check whether any files under app/
# have changed relative to main. If so, and the audit has not yet been
# prompted in this session, block the stop with a directive to invoke the
# souroldgeezer-design:responsive-design skill in review mode (quick) to
# enforce WCAG 2.2 AA, i18n (LTR + RTL + text-expansion), and Core Web
# Vitals baselines on the changed UI.
#
# Sibling of stop-test-audit.sh and stop-devsecops-audit.sh. Uses the same
# session-marker loop-protection pattern — fires at most once per session.
#
# Input: Stop hook JSON on stdin (session_id, cwd, transcript_path, ...).
# Output: {decision: "block", reason: ...} on stdout if any app/ file
#         changed and not yet prompted this session; empty exit 0 otherwise.
#
# Wired up in .claude/settings.json and .codex/hooks.json under hooks.Stop.

set -euo pipefail

hook_name="responsive-audit"

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
marker="$marker_dir/responsive-audit-prompted-${session_id:-unknown}"

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

# ---- detect changed app/ files ----------------------------------------------
# Diff working tree + committed changes on branch against main, restricted to
# the Blazor WASM SPA tree. Includes .razor markup, .css / .js / .html under
# wwwroot, and the .cs code-behind / framework-neutral services under
# app/Lfm.App.Core/ — all of which can affect responsive / a11y / i18n
# behavior. The once-per-session marker caps cost, so the aggressive scope
# is safe.
changed=$(git -C "$repo_root" diff --name-only main -- 'app/' 2>/dev/null || true)

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
    "UI / app files changed in this task:\n\n" + $files + "\n\n" +
    "Before finishing, invoke the `souroldgeezer-design:responsive-design` skill in review mode (quick) on these files. " +
    "Read the skill reference at `souroldgeezer-design/docs/ui-reference/responsive-design.md`, apply the rubric, load the " +
    "Blazor-WASM extension based on the changed paths, and present per-finding output using the rubric fields (severity, " +
    "evidence, action, rubric pointer). Enforce WCAG 2.2 AA, i18n (LTR + RTL + text-expansion), and Core Web Vitals as the " +
    "hard baselines. " +
    "This hook fires once per session — you will not be prompted again after this run."
  )
}'
