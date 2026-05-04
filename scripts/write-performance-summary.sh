#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

browser_report="${1:-artifacts/e2e-results/performance-report.json}"
load_report="${2:-artifacts/e2e-results/performance-load-report.json}"
production_report="${3:-artifacts/production-synthetic/production-synthetic-report.json}"
summary_path="${GITHUB_STEP_SUMMARY:-/dev/stdout}"

if ! command -v jq >/dev/null 2>&1; then
  echo "FAIL: jq is required to write the performance summary." >&2
  exit 2
fi

write_browser_summary() {
  local report="$1"
  if [ ! -f "$report" ]; then
    echo "Local browser report not found: $report"
    return
  fi

  echo "### Local Browser Journeys"
  echo
  echo "| Journey | Viewport | p75 elapsed | p75 LCP | p75 CLS | p75 interaction | p75 API count |"
  echo "|---|---:|---:|---:|---:|---:|---:|"
  jq -r '
    def value($v; $suffix):
      if $v == null then "n/a" else (($v | tostring) + $suffix) end;
    (.Journeys // .journeys // [])[] as $j
    | ($j.Viewport // $j.viewport // {}) as $v
    | ($j.BrowserMetrics // $j.browserMetrics // {}) as $m
    | "| \($j.Name // $j.name) | \($v.Name // $v.name) | \(value($j.P75ElapsedMs // $j.p75ElapsedMs; " ms")) | \(value($m.P75LargestContentfulPaintMs // $m.p75LargestContentfulPaintMs; " ms")) | \(value($m.P75CumulativeLayoutShift // $m.p75CumulativeLayoutShift; "")) | \(value($m.P75ControlledInteractionDurationMs // $m.p75ControlledInteractionDurationMs; " ms")) | \(value($m.P75ApiRequestCount // $m.p75ApiRequestCount; "")) |"
  ' "$report"
}

write_load_summary() {
  local report="$1"
  if [ ! -f "$report" ]; then
    echo "Local load-smoke report not found: $report"
    return
  fi

  echo "### Local Load Smoke"
  echo
  echo "| Group | Probe | Requests | Failures | p75 | Max |"
  echo "|---|---|---:|---:|---:|---:|"
  jq -r '
    def value($v; $suffix):
      if $v == null then "n/a" else (($v | tostring) + $suffix) end;
    (.Probes // .probes // [])[] as $p
    | "| \($p.Group // $p.group // "n/a") | \($p.Name // $p.name) | \($p.RequestCount // $p.requestCount) | \($p.FailureCount // $p.failureCount) | \(value($p.P75Ms // $p.p75Ms; " ms")) | \(value($p.MaxMs // $p.maxMs; " ms")) |"
  ' "$report"
}

write_production_summary() {
  local report="$1"
  if [ ! -f "$report" ]; then
    echo "Production synthetic report not found: $report"
    return
  fi

  echo "### Anonymous Production Synthetic"
  echo
  echo "| Probe | Requests | Failures | p75 | Max |"
  echo "|---|---:|---:|---:|---:|"
  jq -r '
    def value($v; $suffix):
      if $v == null then "n/a" else (($v | tostring) + $suffix) end;
    (.probes // .Probes // [])[] as $p
    | "| \($p.name // $p.Name) | \($p.requestCount // $p.RequestCount) | \($p.failureCount // $p.FailureCount) | \(value($p.p75Ms // $p.P75Ms; " ms")) | \(value($p.maxMs // $p.MaxMs; " ms")) |"
  ' "$report"
}

{
  echo "## Performance Summary"
  echo
  echo "Timing percentiles are advisory unless a report's gate policy says otherwise."
  echo
  write_browser_summary "$browser_report"
  echo
  write_load_summary "$load_report"
  echo
  write_production_summary "$production_report"
} >> "$summary_path"
