#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude Code Stop hook — nudge the responsive-design review skill.
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
marker="$marker_dir/responsive-audit-prompted-${session_id:-unknown}"
[[ -f "$marker" ]] && exit 0

# ---- require main to exist --------------------------------------------------
git -C "$repo_root" rev-parse --verify --quiet main >/dev/null || exit 0

# ---- detect changed app/ files ----------------------------------------------
# Diff working tree + committed changes on branch against main, restricted to
# the Blazor WASM SPA tree. Includes .razor markup, .css / .js / .html under
# wwwroot, and the .cs code-behind / framework-neutral services under
# app/Lfm.App.Core/ — all of which can affect responsive / a11y / i18n
# behavior. The once-per-session marker caps cost, so the aggressive scope
# is safe.
changed=$(git -C "$repo_root" diff --name-only main -- 'app/' 2>/dev/null || true)

[[ -z "$changed" ]] && exit 0

# ---- emit block -------------------------------------------------------------
mkdir -p "$marker_dir"
touch "$marker"

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
