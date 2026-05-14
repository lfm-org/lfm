#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

: "${Cosmos__Endpoint:?Cosmos__Endpoint is required}"
: "${Cosmos__AuthKey:?Cosmos__AuthKey is required}"
: "${Cosmos__DatabaseName:?Cosmos__DatabaseName is required}"

mkdir -p /home/local-secrets
printf '%s' "${Local__SiteAdminBattleNetIds:-}" > /home/local-secrets/site-admin-battle-net-ids

export HOME=/tmp/local-init-home
export DOTNET_CLI_HOME="$HOME"
mkdir -p "$HOME"
cd "$HOME"

endpoint="${Cosmos__Endpoint%/}/"
connection_string="AccountEndpoint=${endpoint};AccountKey=${Cosmos__AuthKey};"

run_cosmosdbshell() {
  local command="$1"
  local output

  if output=$(cosmosdbshell --connect "$connection_string" --connect-mode gateway -c "$command" 2>&1); then
    return 0
  fi

  if printf '%s\n' "$output" | grep -Eiq 'already exists|conflict|409'; then
    return 0
  fi

  printf '%s\n' "${output//${Cosmos__AuthKey}/[redacted]}" >&2
  return 1
}

for attempt in $(seq 1 30); do
  if run_cosmosdbshell "mkdb \"$Cosmos__DatabaseName\"" &&
    run_cosmosdbshell "mkcon raiders /battleNetId --database=\"$Cosmos__DatabaseName\"" &&
    run_cosmosdbshell "mkcon guilds /id --database=\"$Cosmos__DatabaseName\"" &&
    run_cosmosdbshell "mkcon runs /id --database=\"$Cosmos__DatabaseName\"" &&
    run_cosmosdbshell "mkcon idempotency /battleNetId --database=\"$Cosmos__DatabaseName\""
  then
    echo "Local Cosmos containers and site-admin allowlist are ready."
    exit 0
  fi

  echo "Cosmos local init attempt ${attempt} failed; retrying..." >&2
  sleep 2
done

echo "Cosmos local init did not complete before the retry limit." >&2
exit 1
