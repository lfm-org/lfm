#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# Claude Code Stop hook — nudge the test-quality-audit skill.
#
# When Claude is about to finish a turn, check whether any unit test files in
# the repo have changed relative to main. If so, and the audit has not yet
# been prompted in this session, block the stop with a directive to invoke
# the test-quality-audit skill in quick mode.
#
# Input: Stop hook JSON on stdin (session_id, cwd, transcript_path, ...).
# Output: {decision: "block", reason: ...} on stdout if tests changed and
#         not yet prompted this session; empty exit 0 otherwise.
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
marker="$marker_dir/test-audit-prompted-${session_id:-unknown}"
[[ -f "$marker" ]] && exit 0

# ---- require main to exist --------------------------------------------------
git -C "$repo_root" rev-parse --verify --quiet main >/dev/null || exit 0

# ---- detect changed unit test files -----------------------------------------
# Diff working tree + committed changes on branch against main, restricted to
# the unit-test lanes. Excludes E2E (tests/Lfm.E2E/).
changed=$(git -C "$repo_root" diff --name-only main -- \
  'tests/Lfm.Api.Tests' 'tests/Lfm.App.Tests' 'tests/Lfm.App.Core.Tests' 2>/dev/null \
  | grep -E 'Tests?\.cs$' || true)

[[ -z "$changed" ]] && exit 0

# ---- emit block -------------------------------------------------------------
mkdir -p "$marker_dir"
touch "$marker"

jq -n --arg files "$changed" '{
  decision: "block",
  reason: (
    "Unit test files changed in this task:\n\n" + $files + "\n\n" +
    "Before finishing, invoke the `test-quality-audit` skill in quick mode on these files. " +
    "Read skills/test-quality-audit/SKILL.md, apply the rubric, and present per-test findings " +
    "(intent, provenance, smells, verdict, severity, action) in your final response. " +
    "This hook fires once per session — you will not be prompted again after this run."
  )
}'
