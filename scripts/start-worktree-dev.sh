#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/start-worktree-dev.sh [options]

Starts the local Docker stack and Blazor dev server with worktree-safe ports.

Options:
  --offset N                 Add N to the default local ports
  --env-file PATH            Read compose secrets/settings from PATH
  --app-port N               Override the Blazor dev server port
  --functions-port N         Override the Azure Functions host port
  --cosmos-port N            Override the Cosmos emulator port
  --cosmos-explorer-port N   Override the Cosmos explorer port
  --azurite-blob-port N      Override the Azurite blob port
  --compose-only             Start only the Docker compose stack
  --app-only                 Start only the Blazor app
  --keep-compose             Leave compose services running after the app exits
  --no-build                 Do not rebuild compose images before starting
  --print                    Print the computed plan and write appsettings only
  -h, --help                 Show this help

By default, a checkout under .worktrees/<name> gets a deterministic port offset
derived from <name>. The main checkout keeps the documented default ports.
USAGE
}

die() {
  echo "FAIL: $*" >&2
  exit 1
}

is_integer() {
  [[ "$1" =~ ^[0-9]+$ ]]
}

absolute_path() {
  local path="$1"
  local dir
  local base

  dir=$(dirname "$path")
  base=$(basename "$path")
  if [ -d "$dir" ]; then
    printf '%s/%s\n' "$(cd "$dir" && pwd -P)" "$base"
  else
    die "directory not found for path: $path"
  fi
}

sanitize_name() {
  local value="$1"
  local sanitized

  sanitized=$(printf '%s' "$value" |
    tr '[:upper:]' '[:lower:]' |
    tr -cs 'a-z0-9-' '-' |
    sed -e 's/^-//' -e 's/-$//')
  printf '%s\n' "${sanitized:-worktree}"
}

detect_repo_root() {
  local script_dir="$1"

  if git -C "$script_dir" rev-parse --show-toplevel >/dev/null 2>&1; then
    git -C "$script_dir" rev-parse --show-toplevel
    return 0
  fi

  cd "$script_dir/.." && pwd -P
}

default_offset_for() {
  local repo_root="$1"
  local worktree_name="$2"
  local sum

  if [[ "$repo_root" != */.worktrees/* ]]; then
    echo 0
    return 0
  fi

  sum=$(printf '%s' "$worktree_name" | cksum | cut -d ' ' -f 1)
  echo $(( ((sum % 20) + 1) * 100 ))
}

script_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)
repo_root=$(detect_repo_root "$script_dir")
primary_root="$repo_root"
if [[ "$repo_root" == */.worktrees/* ]]; then
  primary_root="${repo_root%%/.worktrees/*}"
fi

worktree_name=$(basename "$repo_root")
worktree_slug=$(sanitize_name "$worktree_name")
base_slug=$(sanitize_name "$(basename "$primary_root")")
if [ "$repo_root" = "$primary_root" ]; then
  compose_project="$base_slug"
else
  compose_project="${base_slug}-${worktree_slug}"
fi

offset="${LFM_DEV_PORT_OFFSET:-$(default_offset_for "$repo_root" "$worktree_name")}"
env_file="${LFM_DEV_ENV_FILE:-}"
app_port="${LFM_DEV_APP_PORT:-}"
functions_port="${LFM_DEV_FUNCTIONS_PORT:-}"
cosmos_port="${LFM_DEV_COSMOS_PORT:-}"
cosmos_explorer_port="${LFM_DEV_COSMOS_EXPLORER_PORT:-}"
azurite_blob_port="${LFM_DEV_AZURITE_BLOB_PORT:-}"
compose_only=false
app_only=false
keep_compose=false
build_compose=true
print_only=false

while [ "$#" -gt 0 ]; do
  case "$1" in
    --offset)
      [ "$#" -ge 2 ] || die "--offset requires a value"
      offset="$2"
      shift 2
      ;;
    --offset=*)
      offset="${1#*=}"
      shift
      ;;
    --env-file)
      [ "$#" -ge 2 ] || die "--env-file requires a value"
      env_file="$2"
      shift 2
      ;;
    --env-file=*)
      env_file="${1#*=}"
      shift
      ;;
    --app-port)
      [ "$#" -ge 2 ] || die "--app-port requires a value"
      app_port="$2"
      shift 2
      ;;
    --app-port=*)
      app_port="${1#*=}"
      shift
      ;;
    --functions-port)
      [ "$#" -ge 2 ] || die "--functions-port requires a value"
      functions_port="$2"
      shift 2
      ;;
    --functions-port=*)
      functions_port="${1#*=}"
      shift
      ;;
    --cosmos-port)
      [ "$#" -ge 2 ] || die "--cosmos-port requires a value"
      cosmos_port="$2"
      shift 2
      ;;
    --cosmos-port=*)
      cosmos_port="${1#*=}"
      shift
      ;;
    --cosmos-explorer-port)
      [ "$#" -ge 2 ] || die "--cosmos-explorer-port requires a value"
      cosmos_explorer_port="$2"
      shift 2
      ;;
    --cosmos-explorer-port=*)
      cosmos_explorer_port="${1#*=}"
      shift
      ;;
    --azurite-blob-port)
      [ "$#" -ge 2 ] || die "--azurite-blob-port requires a value"
      azurite_blob_port="$2"
      shift 2
      ;;
    --azurite-blob-port=*)
      azurite_blob_port="${1#*=}"
      shift
      ;;
    --compose-only | --no-app)
      compose_only=true
      shift
      ;;
    --app-only | --no-compose)
      app_only=true
      shift
      ;;
    --keep-compose)
      keep_compose=true
      shift
      ;;
    --no-build)
      build_compose=false
      shift
      ;;
    --print)
      print_only=true
      shift
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      die "unknown option: $1"
      ;;
  esac
done

is_integer "$offset" || die "offset must be a non-negative integer: $offset"

: "${app_port:=$((5138 + offset))}"
: "${functions_port:=$((7071 + offset))}"
: "${cosmos_port:=$((8081 + offset))}"
: "${cosmos_explorer_port:=$((1234 + offset))}"
: "${azurite_blob_port:=$((10000 + offset))}"

for named_port in \
  "app-port:$app_port" \
  "functions-port:$functions_port" \
  "cosmos-port:$cosmos_port" \
  "cosmos-explorer-port:$cosmos_explorer_port" \
  "azurite-blob-port:$azurite_blob_port"
do
  port_name="${named_port%%:*}"
  port_value="${named_port#*:}"
  is_integer "$port_value" || die "$port_name must be a non-negative integer: $port_value"
done

if [ "$compose_only" = true ] && [ "$app_only" = true ]; then
  die "--compose-only and --app-only cannot be used together"
fi

if [ -z "$env_file" ]; then
  if [ -f "$repo_root/.env" ]; then
    env_file="$repo_root/.env"
  elif [ -f "$primary_root/.env" ]; then
    env_file="$primary_root/.env"
  else
    die "no .env found; copy example.env to .env in $repo_root or $primary_root, or pass --env-file"
  fi
fi
env_file=$(absolute_path "$env_file")
[ -f "$env_file" ] || die "env file not found: $env_file"

app_url="http://localhost:${app_port}"
api_url="http://localhost:${functions_port}"
appsettings_path="$repo_root/app/wwwroot/appsettings.Development.json"
compose_file="$repo_root/docker-compose.local.yml"

command -v jq >/dev/null 2>&1 || die "jq is required"

write_appsettings() {
  mkdir -p "$(dirname "$appsettings_path")"
  jq -n --arg apiBaseUrl "$api_url" \
    '{ApiBaseUrl: $apiBaseUrl}' > "$appsettings_path"
}

print_plan() {
  cat <<PLAN
Worktree: $repo_root
Compose project: $compose_project
Env file: $env_file
App URL: $app_url
API URL: $api_url
Cosmos emulator: http://localhost:$cosmos_port
Cosmos explorer: http://localhost:$cosmos_explorer_port
Azurite blob: http://localhost:$azurite_blob_port
App settings: $appsettings_path
PLAN
}

write_appsettings
print_plan

if [ "$print_only" = true ]; then
  exit 0
fi

compose_args=(
  docker compose
  --env-file "$env_file"
  -f "$compose_file"
  -p "$compose_project"
)

export COSMOS_PORT="$cosmos_port"
export COSMOS_EXPLORER_PORT="$cosmos_explorer_port"
export AZURITE_BLOB_PORT="$azurite_blob_port"
export FUNCTIONS_PORT="$functions_port"
export Cors__AllowedOrigins__0="$app_url"
export Blizzard__RedirectUri="${api_url}/api/battlenet/callback"
export Blizzard__AppBaseUrl="$app_url"

if [ "$app_only" = false ]; then
  command -v docker >/dev/null 2>&1 || die "docker is required"
  compose_up=(up -d)
  if [ "$build_compose" = true ]; then
    compose_up+=(--build)
  fi
  "${compose_args[@]}" "${compose_up[@]}"
fi

cleanup_compose=false
if [ "$compose_only" = false ] && [ "$app_only" = false ] && [ "$keep_compose" = false ]; then
  cleanup_compose=true
fi

cleanup() {
  if [ "$cleanup_compose" = true ]; then
    "${compose_args[@]}" down
  fi
}
trap cleanup EXIT

if [ "$compose_only" = true ]; then
  echo "Compose stack is running. Stop it with:"
  printf '  docker compose --env-file %q -f %q -p %q down\n' \
    "$env_file" "$compose_file" "$compose_project"
  exit 0
fi

command -v dotnet >/dev/null 2>&1 || die "dotnet is required"
ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS="$app_url" \
  dotnet run --project "$repo_root/app/Lfm.App.csproj" \
  --no-launch-profile
