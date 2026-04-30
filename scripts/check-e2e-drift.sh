#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

status=0

check_absent() {
  local description="$1"
  local pattern="$2"
  shift 2

  if rg -n "$pattern" "$@"; then
    printf 'E2E drift check failed: %s\n' "$description" >&2
    status=1
  fi
}

check_review() {
  local description="$1"
  local pattern="$2"
  shift 2

  if rg -n "$pattern" "$@"; then
    printf 'E2E drift review: %s\n' "$description" >&2
  fi
}

check_absent \
  "legacy unversioned app API route in E2E specs/helpers/page objects" \
  '"/api/(battlenet|wow|raiders|runs|guilds|me)(/|[?"])' \
  tests/Lfm.E2E/Specs \
  tests/Lfm.E2E/Helpers \
  tests/Lfm.E2E/Pages

check_absent \
  "Blizzard wire keys in Cosmos raider seed documents" \
  'wow_accounts|playable_class' \
  tests/Lfm.E2E/Seeds/DefaultSeed.cs \
  tests/Lfm.E2E/Seeds/RaiderSeedBuilder.cs

check_absent \
  "stale run-form selector in E2E specs" \
  'modekey-input|#instance-select' \
  tests/Lfm.E2E/Specs

check_absent \
  "removed mode-key selector in E2E page objects" \
  'modekey-input' \
  tests/Lfm.E2E/Pages

check_review \
  "page-object #instance-select usage must match CreateRunPage/EditRunPage Id" \
  '#instance-select' \
  tests/Lfm.E2E/Pages

check_review \
  "extra pages must either attach diagnostics or be explicitly closed" \
  '\bvar [A-Za-z0-9_]+ = await .*NewPageAsync\(' \
  tests/Lfm.E2E/Specs

check_review \
  "destructive tests must use disposable identities or restore shared data" \
  'DeleteAsync|DELETE|me-delete|DeleteAccount' \
  tests/Lfm.E2E/Specs \
  tests/Lfm.E2E/Seeds

exit "$status"
