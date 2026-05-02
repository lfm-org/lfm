#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

repo_root="$(git -C "$(dirname "${BASH_SOURCE[0]}")/.." rev-parse --show-toplevel)"
state_file="$repo_root/docs/api-alias-retirement.json"

legacy_aliases_allowed="$(jq -r '.legacyAliasesAllowed' "$state_file")"
mapfile -t legacy_aliases < <(
  rg --no-heading --line-number --pcre2 \
    'Route = "(?!v1/|e2e/|\{\*path\})[^"]+"' \
    "$repo_root/api/Functions" \
    -g '*.cs' \
    | sort
)

if [[ "$legacy_aliases_allowed" == "true" ]]; then
  printf 'Legacy API aliases are still allowed by %s; detected %d transitional route declarations.\n' \
    "$state_file" "${#legacy_aliases[@]}"
  exit 0
fi

if [[ "$legacy_aliases_allowed" != "false" ]]; then
  printf 'Invalid legacyAliasesAllowed value in %s: %s\n' "$state_file" "$legacy_aliases_allowed" >&2
  exit 2
fi

if ((${#legacy_aliases[@]} > 0)); then
  printf 'legacyAliasesAllowed=false, but unprefixed API route declarations remain:\n' >&2
  printf '%s\n' "${legacy_aliases[@]}" >&2
  exit 1
fi

printf 'legacyAliasesAllowed=false and no unprefixed production API route declarations remain.\n'
