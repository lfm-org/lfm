#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

repo_root=$(git -C "$(dirname "$0")" rev-parse --show-toplevel)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

fixture="$tmp/lfm"
worktree="$fixture/.worktrees/feature-a"
mkdir -p "$fixture/app/wwwroot" "$fixture/scripts" "$worktree/app/wwwroot" "$worktree/scripts"
cp "$repo_root/scripts/start-worktree-dev.sh" "$fixture/scripts/start-worktree-dev.sh"
cp "$repo_root/scripts/start-worktree-dev.sh" "$worktree/scripts/start-worktree-dev.sh"
touch "$fixture/.env"

main_output=$(bash "$fixture/scripts/start-worktree-dev.sh" --print --offset 0)
output=$(bash "$worktree/scripts/start-worktree-dev.sh" --print --offset 100)

[[ "$main_output" == *"Compose project: lfm"* ]]
[[ "$main_output" == *"Env file: $fixture/.env"* ]]
[[ "$main_output" == *"App URL: http://localhost:5138"* ]]

[[ "$output" == *"Compose project: lfm-feature-a"* ]]
[[ "$output" == *"Env file: $fixture/.env"* ]]
[[ "$output" == *"App URL: http://localhost:5238"* ]]
[[ "$output" == *"API URL: http://localhost:7171"* ]]
[[ "$output" == *"Cosmos explorer: http://localhost:1334"* ]]
[[ "$output" == *"Azurite blob: http://localhost:10100"* ]]

jq -e '.ApiBaseUrl == "http://localhost:7171"' \
  "$worktree/app/wwwroot/appsettings.Development.json" >/dev/null
