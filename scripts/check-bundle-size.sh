#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

PUBLISH_DIR="${1:-./publish/app/wwwroot}"
BUDGET_MB="${2:-2}"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "FAIL: publish directory not found: $PUBLISH_DIR" >&2
  exit 2
fi

if ! command -v brotli >/dev/null 2>&1; then
  echo "FAIL: brotli not installed (apt-get install brotli)" >&2
  exit 2
fi

# Assets SWA will serve. We measure brotli because SWA negotiates br > gzip > identity.
mapfile -t assets < <(find "$PUBLISH_DIR" \
  -type f \( -name '*.js' -o -name '*.wasm' -o -name '*.dat' -o -name '*.dll' -o -name '*.css' -o -name '*.html' \) \
  ! -name '*.br' ! -name '*.gz' | sort)

total_bytes=0
tmp_breakdown=$(mktemp)
trap 'rm -f "$tmp_breakdown"' EXIT

for asset in "${assets[@]}"; do
  if [ -f "${asset}.br" ]; then
    size=$(wc -c < "${asset}.br")
  else
    size=$(brotli --quality=11 --stdout "$asset" | wc -c)
  fi
  total_bytes=$(( total_bytes + size ))
  printf '%10d  %s\n' "$size" "${asset#$PUBLISH_DIR/}" >> "$tmp_breakdown"
done

budget_bytes=$(( BUDGET_MB * 1024 * 1024 ))
total_mb=$(awk "BEGIN {printf \"%.2f\", $total_bytes/1024/1024}")

echo "Total brotli bundle: ${total_mb} MB (budget: ${BUDGET_MB} MB, ${#assets[@]} assets)"

if [ "$total_bytes" -gt "$budget_bytes" ]; then
  echo "FAIL: bundle exceeds ${BUDGET_MB} MB budget" >&2
  echo "Top 10 largest assets (brotli bytes):" >&2
  sort -rn "$tmp_breakdown" | head -10 >&2
  exit 1
fi
