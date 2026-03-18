#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FRONTEND_DIR="$ROOT_DIR/frontend"
COMPOSE_FILE="$ROOT_DIR/docker-compose.test.yml"
TMP_DIR="$ROOT_DIR/.tmp/e2e"
PLAYWRIGHT_BROWSERS_PATH="${PLAYWRIGHT_BROWSERS_PATH:-$ROOT_DIR/.cache/ms-playwright}"
E2E_KEEP_DOCKER="${E2E_KEEP_DOCKER:-0}"

FUNCTIONS_PORT="${FUNCTIONS_PORT:-7071}"
FRONTEND_PORT="${FRONTEND_PORT:-4173}"
PLAYWRIGHT_BASE_URL="${PLAYWRIGHT_BASE_URL:-http://127.0.0.1:${FRONTEND_PORT}}"
COSMOS_KEY="${COSMOS_KEY:-C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==}"
COSMOS_KEY_FILE="$TMP_DIR/cosmos.key"
AZURITE_ACCOUNT_KEY="${AZURITE_ACCOUNT_KEY:-Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==}"
COSMOS_ENDPOINT_INTERNAL="${COSMOS_ENDPOINT_INTERNAL:-http://cosmosdb:8081}"
AZURE_BLOB_ENDPOINT_INTERNAL="${AZURE_BLOB_ENDPOINT_INTERNAL:-http://azurite:10000/devstoreaccount1}"
AZURE_WEBJOBS_STORAGE_INTERNAL="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=${AZURITE_ACCOUNT_KEY};BlobEndpoint=${AZURE_BLOB_ENDPOINT_INTERNAL};"
TOKEN_ENCRYPTION_KEY="${TOKEN_ENCRYPTION_KEY:-00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff}"
HMAC_SECRET="${HMAC_SECRET:-0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef}"

mkdir -p "$TMP_DIR" "$TMP_DIR/azurite"
printf '%s' "$COSMOS_KEY" > "$COSMOS_KEY_FILE"

cleanup() {
  local exit_code=$?

  if [[ "$E2E_KEEP_DOCKER" != "1" ]]; then
    docker compose -f "$COMPOSE_FILE" down --remove-orphans >/dev/null 2>&1 || true
  fi

  return "$exit_code"
}

trap cleanup EXIT

wait_for_port() {
  local host="$1"
  local port="$2"
  local attempts="${3:-60}"

  for ((i = 0; i < attempts; i++)); do
    if bash -c "exec 3<>/dev/tcp/${host}/${port}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done

  echo "Timed out waiting for ${host}:${port}" >&2
  return 1
}

wait_for_http() {
  local url="$1"
  local attempts="${2:-60}"

  for ((i = 0; i < attempts; i++)); do
    if curl --silent --show-error --fail "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done

  echo "Timed out waiting for ${url}" >&2
  return 1
}

run_with_retry() {
  local description="$1"
  local attempts="$2"
  shift 2

  for ((i = 1; i <= attempts; i++)); do
    if "$@"; then
      return 0
    fi

    if [[ "$i" -lt "$attempts" ]]; then
      echo "${description} failed on attempt ${i}/${attempts}; retrying..." >&2
      sleep 5
    fi
  done

  echo "${description} failed after ${attempts} attempts" >&2
  return 1
}

export TEST_MODE=true
export COSMOS_ENDPOINT="$COSMOS_ENDPOINT_INTERNAL"
export COSMOS_KEY
export COSMOS_DATABASE=sisu-raidcal
export AzureWebJobsStorage="$AZURE_WEBJOBS_STORAGE_INTERNAL"
export BLOB_STORAGE_URL="$AZURE_BLOB_ENDPOINT_INTERNAL"
export APP_BASE_URL="$PLAYWRIGHT_BASE_URL"
export COOKIE_DOMAIN=127.0.0.1
export BATTLE_NET_COOKIE_SECURE=false
export BATTLE_NET_REDIRECT_URI="http://127.0.0.1:${FUNCTIONS_PORT}/api/battlenet/callback"
export BATTLE_NET_REGION=eu
export SISU_RAIDCAL_CLIENT_ID=""
export SISU_RAIDCAL_CLIENT_SECRET=""
export TOKEN_ENCRYPTION_KEY
export HMAC_SECRET

docker compose -f "$COMPOSE_FILE" up -d cosmosdb azurite
wait_for_port 127.0.0.1 8081 120
wait_for_port 127.0.0.1 10000 120

docker compose -f "$COMPOSE_FILE" build functions
docker compose -f "$COMPOSE_FILE" run --rm --entrypoint node functions dist/src/scripts/load-test-reference-data.js
run_with_retry \
  "seed test data" \
  12 \
  docker compose -f "$COMPOSE_FILE" run --rm --entrypoint node functions dist/src/scripts/seed-test-data.js
docker compose -f "$COMPOSE_FILE" up -d functions

wait_for_http "http://127.0.0.1:${FUNCTIONS_PORT}/api/health" 120

PLAYWRIGHT_ARGS=()
if [[ $# -gt 0 ]]; then
  if [[ "$1" != -* && "$1" != */* && "$1" != *.spec.ts ]]; then
    PLAYWRIGHT_ARGS+=("e2e/${1}.spec.ts")
    shift
  fi
  PLAYWRIGHT_ARGS+=("$@")
fi

(
  cd "$FRONTEND_DIR"
  export PLAYWRIGHT_BASE_URL
  export PLAYWRIGHT_BROWSERS_PATH
  export VITE_PROXY_TARGET="http://127.0.0.1:${FUNCTIONS_PORT}"
  export VITE_API_BASE_URL=/api
  npx playwright install chromium
  npx playwright test "${PLAYWRIGHT_ARGS[@]}"
)
