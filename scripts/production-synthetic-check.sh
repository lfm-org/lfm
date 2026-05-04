#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors
#
# Runs low-volume anonymous production synthetic checks and writes a JSON report.
# Intended for scheduled performance evidence, not load testing.

set -euo pipefail

timeout_seconds="${SYNTHETIC_TIMEOUT_SECONDS:-10}"
output_path="${SYNTHETIC_OUTPUT:-artifacts/production-synthetic/production-synthetic-report.json}"

if ! command -v jq >/dev/null 2>&1; then
  echo "FAIL: jq is required to write the synthetic report." >&2
  exit 2
fi

case "$timeout_seconds" in
  ''|*[!0-9]*)
    echo "FAIL: SYNTHETIC_TIMEOUT_SECONDS must be an integer." >&2
    exit 2
    ;;
esac

normalize_base_url() {
  local value="${1%/}"
  if [[ "$value" == http://* || "$value" == https://* ]]; then
    printf '%s' "$value"
  else
    printf 'https://%s' "$value"
  fi
}

frontend_base_url="${FRONTEND_BASE_URL:-}"
api_base_url="${API_BASE_URL:-}"

if [ -z "$frontend_base_url" ]; then
  frontend_host="${FRONTEND_HOSTNAME:-}"
  if [ -z "$frontend_host" ]; then
    echo "FAIL: set FRONTEND_BASE_URL or FRONTEND_HOSTNAME." >&2
    exit 2
  fi
  frontend_base_url="$(normalize_base_url "$frontend_host")"
else
  frontend_base_url="$(normalize_base_url "$frontend_base_url")"
fi

if [ -z "$api_base_url" ]; then
  api_host="${API_HOSTNAME:-}"
  if [ -z "$api_host" ]; then
    echo "FAIL: set API_BASE_URL or API_HOSTNAME." >&2
    exit 2
  fi
  api_base_url="$(normalize_base_url "$api_host")"
else
  api_base_url="$(normalize_base_url "$api_base_url")"
fi

output_dir="$(dirname "$output_path")"
mkdir -p "$output_dir"

samples_file="${output_dir}/.production-synthetic-samples-$$.json"
curl_error_file="${output_dir}/.production-synthetic-curl-$$.err"
trap 'rm -f "$samples_file" "$samples_file.tmp" "$curl_error_file"' EXIT
printf '[]\n' > "$samples_file"

append_sample() {
  local name="$1"
  local url="$2"
  local expected_status="$3"
  local status_code="$4"
  local elapsed_ms="$5"
  local success="$6"
  local error="$7"
  local status_json="null"

  if [[ "$status_code" =~ ^[0-9]+$ ]]; then
    status_json="$status_code"
  fi

  jq \
    --arg name "$name" \
    --arg method "GET" \
    --arg url "$url" \
    --argjson expectedStatus "$expected_status" \
    --argjson statusCode "$status_json" \
    --argjson elapsedMs "$elapsed_ms" \
    --argjson success "$success" \
    --arg error "$error" \
    '. += [{
      name: $name,
      method: $method,
      url: $url,
      expectedStatus: $expectedStatus,
      statusCode: $statusCode,
      elapsedMs: $elapsedMs,
      success: $success,
      error: ($error | if length == 0 then null else . end)
    }]' "$samples_file" > "$samples_file.tmp"
  mv "$samples_file.tmp" "$samples_file"
}

probes=(
  "frontend-root|${frontend_base_url}/|200"
  "api-health|${api_base_url}/api/health|200"
  "api-v1-health|${api_base_url}/api/v1/health|200"
)

for probe in "${probes[@]}"; do
  IFS='|' read -r name url expected_status <<< "$probe"
  start_ns="$(date +%s%N)"
  status_code=""
  error=""
  success=false

  if status_code="$(curl \
    --silent \
    --show-error \
    --location \
    --max-time "$timeout_seconds" \
    --output /dev/null \
    --write-out "%{http_code}" \
    "$url" 2>"$curl_error_file")"; then
    if [ "$status_code" = "$expected_status" ]; then
      success=true
    else
      error="expected HTTP ${expected_status}, got ${status_code}"
    fi
  else
    status_code="${status_code:-000}"
    error="$(tr '\n' ' ' < "$curl_error_file")"
  fi

  end_ns="$(date +%s%N)"
  elapsed_ms=$(((end_ns - start_ns) / 1000000))
  append_sample "$name" "$url" "$expected_status" "$status_code" "$elapsed_ms" "$success" "$error"
done

generated_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
request_count="${#probes[@]}"
failure_count="$(jq '[.[] | select(.success != true)] | length' "$samples_file")"

jq -n \
  --arg generatedAt "$generated_at" \
  --arg policy "Anonymous production availability only; low volume and not a load test, capacity test, or production SLO." \
  --argjson timeoutSeconds "$timeout_seconds" \
  --argjson requestCount "$request_count" \
  --argjson failureCount "$failure_count" \
  --slurpfile probes "$samples_file" \
  '{
    schemaVersion: 1,
    generatedAt: $generatedAt,
    policy: $policy,
    run: {
      target: "production",
      timeoutSeconds: $timeoutSeconds,
      requestCount: $requestCount,
      failureCount: $failureCount
    },
    probes: $probes[0]
  }' > "$output_path"

jq '.' "$output_path"

if [ "$failure_count" -gt 0 ]; then
  echo "FAIL: production synthetic check had ${failure_count} failure(s)." >&2
  exit 1
fi

echo "Production synthetic check passed (${request_count} anonymous requests)."
