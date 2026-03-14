#!/usr/bin/env bash
set -euo pipefail

COMPOSE="docker compose -f docker-compose.test.yml"

cleanup() {
  $COMPOSE down --volumes
}
trap cleanup EXIT

$COMPOSE up --build --abort-on-container-exit --exit-code-from playwright
