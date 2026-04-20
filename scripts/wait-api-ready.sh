#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors
#
# Polls a health-check URL until it returns HTTP 200, up to a 120-second
# ceiling. Used after Azure Functions deploy to confirm the API is warm.
#
# Usage: scripts/wait-api-ready.sh <url> [attempts] [sleep-seconds]

set -euo pipefail

url="${1:?url required}"
attempts="${2:-12}"
sleep_seconds="${3:-10}"

for i in $(seq 1 "$attempts"); do
  status=$(curl -s -o /dev/null -w "%{http_code}" "$url" || echo "000")
  if [ "$status" = "200" ]; then
    echo "Health check passed (attempt $i)"
    exit 0
  fi
  echo "Attempt $i/$attempts: got $status, retrying in ${sleep_seconds}s..."
  sleep "$sleep_seconds"
done

echo "Health check failed after $((attempts * sleep_seconds))s" >&2
exit 1
