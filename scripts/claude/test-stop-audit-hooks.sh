#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

repo_root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

make_fixture() {
  local fixture=$1
  mkdir -p "$fixture/scripts/claude" \
    "$fixture/tests/Lfm.Api.Tests" \
    "$fixture/api/Functions" \
    "$fixture/app/Pages"

  cp "$repo_root"/scripts/claude/stop-*-audit.sh "$fixture/scripts/claude/"
  touch "$fixture/lfm.sln" \
    "$fixture/tests/Lfm.Api.Tests/SampleTests.cs" \
    "$fixture/api/Functions/SampleFunction.cs" \
    "$fixture/app/Pages/SamplePage.razor"

  git -C "$fixture" init -q -b main
  git -C "$fixture" config user.email "hook-test@example.invalid"
  git -C "$fixture" config user.name "Hook Test"
  git -C "$fixture" add .
  git -C "$fixture" commit -q -m "baseline"
  git -C "$fixture" switch -q -c agents/hook-test

  printf '// changed\n' >>"$fixture/tests/Lfm.Api.Tests/SampleTests.cs"
  printf '// changed\n' >>"$fixture/api/Functions/SampleFunction.cs"
  printf '@* changed *@\n' >>"$fixture/app/Pages/SamplePage.razor"
}

hook_input() {
  local cwd=$1
  local session_id=$2
  local active=$3
  jq -n \
    --arg cwd "$cwd" \
    --arg session_id "$session_id" \
    --argjson stop_hook_active "$active" \
    '{
      session_id: $session_id,
      cwd: $cwd,
      hook_event_name: "Stop",
      stop_hook_active: $stop_hook_active,
      last_assistant_message: "base"
    }'
}

assert_block() {
  local output=$1
  local needle=$2
  [[ "$(jq -r '.decision' <<<"$output")" == "block" ]]
  jq -e --arg needle "$needle" '.reason | contains($needle)' <<<"$output" >/dev/null
}

fixture="$tmp/repo"
make_fixture "$fixture"

test_output=$(hook_input "$fixture" "test-agent-hooks" false |
  AGENT_HOOK_DEBUG=1 bash "$fixture/scripts/claude/stop-test-audit.sh")
devsecops_output=$(hook_input "$fixture" "devsecops-agent-hooks" false |
  AGENT_HOOK_DEBUG=1 bash "$fixture/scripts/claude/stop-devsecops-audit.sh")
responsive_output=$(hook_input "$fixture" "responsive-agent-hooks" false |
  AGENT_HOOK_DEBUG=1 bash "$fixture/scripts/claude/stop-responsive-audit.sh")
software_design_output=$(hook_input "$fixture" "software-design-agent-hooks" false |
  AGENT_HOOK_DEBUG=1 bash "$fixture/scripts/claude/stop-software-design-audit.sh")

assert_block "$test_output" "Unit test files changed"
assert_block "$devsecops_output" "Security-relevant files changed"
assert_block "$responsive_output" "UI / app files changed"
assert_block "$software_design_output" "Source / project / shell-script files changed"

blocked_debug_fixture="$tmp/blocked-debug-repo"
make_fixture "$blocked_debug_fixture"
mkdir -p "$blocked_debug_fixture/.cache/agent-hooks/debug.jsonl"

blocked_debug_stderr="$tmp/blocked-debug.stderr"
blocked_debug_output=$(hook_input "$blocked_debug_fixture" "blocked-debug" false |
  AGENT_HOOK_DEBUG=1 bash "$blocked_debug_fixture/scripts/claude/stop-test-audit.sh" 2>"$blocked_debug_stderr")
assert_block "$blocked_debug_output" "Unit test files changed"
[[ ! -s "$blocked_debug_stderr" ]]

[[ -f "$fixture/.cache/agent-hooks/test-audit-prompted-test-agent-hooks" ]]
[[ -f "$fixture/.cache/agent-hooks/devsecops-audit-prompted-devsecops-agent-hooks" ]]
[[ -f "$fixture/.cache/agent-hooks/responsive-audit-prompted-responsive-agent-hooks" ]]
[[ -f "$fixture/.cache/agent-hooks/software-design-audit-prompted-software-design-agent-hooks" ]]
[[ ! -d "$fixture/.cache/claude-hooks" ]]

debug_log="$fixture/.cache/agent-hooks/debug.jsonl"
[[ -f "$debug_log" ]]
jq -e 'select(.hook == "test-audit" and .event == "emit-block")' "$debug_log" >/dev/null
jq -e 'select(.hook == "devsecops-audit" and .event == "emit-block")' "$debug_log" >/dev/null
jq -e 'select(.hook == "responsive-audit" and .event == "emit-block")' "$debug_log" >/dev/null
jq -e 'select(.hook == "software-design-audit" and .event == "emit-block")' "$debug_log" >/dev/null

stop_active_output=$(hook_input "$fixture" "new-session" true |
  AGENT_HOOK_DEBUG=1 bash "$fixture/scripts/claude/stop-test-audit.sh")
[[ -z "$stop_active_output" ]]
[[ ! -f "$fixture/.cache/agent-hooks/test-audit-prompted-new-session" ]]
jq -e 'select(.hook == "test-audit" and .event == "skip-stop-hook-active")' "$debug_log" >/dev/null

jq -e '
  [.hooks.Stop[].hooks[].statusMessage] as $messages
  | ($messages | length) == 3
  and all($messages[]; type == "string" and length > 0)
' "$repo_root/.codex/hooks.json" >/dev/null
