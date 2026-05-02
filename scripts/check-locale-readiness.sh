#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
LOCALES_DIR="$REPO_ROOT/app/wwwroot/locales"
READINESS_DIR="$REPO_ROOT/docs/locale-readiness"

current_ready_locales=(en "fi")
required_sections=(
  "Responsive sweep"
  "Text expansion"
  "Locale parity"
  "Bidi/RTL"
  "Collation"
)

is_current_ready_locale() {
  local locale="$1"
  local ready
  for ready in "${current_ready_locales[@]}"; do
    if [[ "$locale" == "$ready" ]]; then
      return 0
    fi
  done
  return 1
}

shopt -s nullglob

failures=0
for locale_file in "$LOCALES_DIR"/*.json; do
  locale="$(basename "$locale_file" .json)"
  if is_current_ready_locale "$locale"; then
    continue
  fi

  evidence_file="$READINESS_DIR/$locale.md"
  if [[ ! -f "$evidence_file" ]]; then
    echo "FAIL: locale '$locale' requires readiness evidence at docs/locale-readiness/$locale.md" >&2
    failures=1
    continue
  fi

  for section in "${required_sections[@]}"; do
    if ! grep -Fqi "$section" "$evidence_file"; then
      echo "FAIL: docs/locale-readiness/$locale.md is missing '$section' evidence" >&2
      failures=1
    fi
  done
done

if ((failures > 0)); then
  echo "Locale readiness check failed." >&2
  echo "Current ready scope is en/fi LTR. Add the required readiness evidence before introducing another locale." >&2
  exit 1
fi

echo "Locale readiness check passed."
