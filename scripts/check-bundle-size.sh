#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

PUBLISH_DIR="${1:-./publish/app/wwwroot}"
BUDGET_MB="${2:-2}"
OUTPUT_DIR="${3:-./artifacts/bundle-size}"
BASELINE_PATH="${BUNDLE_BASELINE_PATH:-docs/testing/bundle-size-baseline.json}"
GROWTH_WARNING_PERCENT="${BUNDLE_GROWTH_WARNING_PERCENT:-10}"

if [ ! -d "$PUBLISH_DIR" ]; then
  echo "FAIL: publish directory not found: $PUBLISH_DIR" >&2
  exit 2
fi

if ! command -v brotli >/dev/null 2>&1; then
  echo "FAIL: brotli not installed (apt-get install brotli)" >&2
  exit 2
fi

if ! command -v jq >/dev/null 2>&1; then
  echo "FAIL: jq not installed" >&2
  exit 2
fi

# Assets SWA will serve. We measure brotli because SWA negotiates br > gzip > identity.
mapfile -t assets < <(find "$PUBLISH_DIR" \
  -type f \( -name '*.js' -o -name '*.wasm' -o -name '*.dat' -o -name '*.dll' -o -name '*.css' -o -name '*.html' \) \
  ! -name '*.br' ! -name '*.gz' | sort)

total_bytes=0
mkdir -p "$OUTPUT_DIR"
tmp_breakdown=$(mktemp "$OUTPUT_DIR/bundle-size.XXXXXX")
trap 'rm -f "$tmp_breakdown"' EXIT
breakdown_file="$OUTPUT_DIR/bundle-size-breakdown.txt"
report_file="$OUTPUT_DIR/bundle-size-report.json"

for asset in "${assets[@]}"; do
  if [ -f "${asset}.br" ]; then
    size=$(wc -c < "${asset}.br")
  else
    size=$(brotli --quality=11 --stdout "$asset" | wc -c)
  fi
  total_bytes=$(( total_bytes + size ))
  printf '%10d  %s\n' "$size" "${asset#"$PUBLISH_DIR"/}" >> "$tmp_breakdown"
done

sort -rn "$tmp_breakdown" > "$breakdown_file"

budget_bytes=$(( BUDGET_MB * 1024 * 1024 ))
total_mb=$(awk "BEGIN {printf \"%.2f\", $total_bytes/1024/1024}")
baseline_bytes=""
growth_warning=false
growth_percent_json=null

if [ -f "$BASELINE_PATH" ]; then
  baseline_bytes=$(jq -r '.totalBrotliBytes // empty' "$BASELINE_PATH")
  if [ -n "$baseline_bytes" ] && [ "$baseline_bytes" -gt 0 ]; then
    threshold_bytes=$(( baseline_bytes * (100 + GROWTH_WARNING_PERCENT) / 100 ))
    growth_percent=$(awk "BEGIN {printf \"%.1f\", (($total_bytes-$baseline_bytes)*100)/$baseline_bytes}")
    growth_percent_json="$growth_percent"
    if [ "$total_bytes" -gt "$threshold_bytes" ]; then
      growth_warning=true
    fi
  fi
fi

baseline_bytes_json=null
if [ -n "$baseline_bytes" ]; then
  baseline_bytes_json="$baseline_bytes"
fi

jq -n \
  --arg generatedAt "$(date -u +"%Y-%m-%dT%H:%M:%SZ")" \
  --arg baselinePath "$BASELINE_PATH" \
  --argjson totalBrotliBytes "$total_bytes" \
  --argjson budgetBytes "$budget_bytes" \
  --argjson assetCount "${#assets[@]}" \
  --argjson baselineTotalBrotliBytes "$baseline_bytes_json" \
  --argjson growthWarningPercent "$GROWTH_WARNING_PERCENT" \
  --argjson growthPercent "$growth_percent_json" \
  --argjson growthWarning "$growth_warning" \
  --rawfile breakdown "$breakdown_file" \
  '{
    generatedAt: $generatedAt,
    totalBrotliBytes: $totalBrotliBytes,
    budgetBytes: $budgetBytes,
    assetCount: $assetCount,
    baselinePath: $baselinePath,
    baselineTotalBrotliBytes: $baselineTotalBrotliBytes,
    growthWarningPercent: $growthWarningPercent,
    growthPercent: $growthPercent,
    growthWarning: $growthWarning,
    assets: (
      $breakdown
      | split("\n")
      | map(select(length > 0))
      | map(capture("^\\s*(?<brotliBytes>\\d+)\\s+(?<path>.*)$") | {path, brotliBytes: (.brotliBytes | tonumber)})
    )
  }' > "$report_file"

echo "Total brotli bundle: ${total_mb} MB (budget: ${BUDGET_MB} MB, ${#assets[@]} assets)"
echo "Bundle breakdown: $breakdown_file"
echo "Bundle report: $report_file"

if [ "$growth_warning" = true ]; then
  echo "WARN: bundle is ${growth_percent}% over baseline (${baseline_bytes} bytes) with warning threshold ${GROWTH_WARNING_PERCENT}%"
elif [ -n "$baseline_bytes" ]; then
  echo "Bundle growth vs baseline: ${growth_percent_json}%"
else
  echo "Bundle baseline not found at $BASELINE_PATH; growth warning skipped"
fi

echo "Top 10 largest assets (brotli bytes):"
head -10 "$breakdown_file"

if [ "${BUNDLE_UPDATE_BASELINE:-false}" = "1" ]; then
  jq '{
    generatedAt,
    totalBrotliBytes,
    budgetBytes,
    assetCount,
    assets: .assets[:10],
    note: "Baseline for scripts/check-bundle-size.sh growth warnings; update only after intentional bundle changes."
  }' "$report_file" > "$BASELINE_PATH"
  echo "Updated baseline: $BASELINE_PATH"
fi

if [ "$total_bytes" -gt "$budget_bytes" ]; then
  echo "FAIL: bundle exceeds ${BUDGET_MB} MB budget" >&2
  exit 1
fi
