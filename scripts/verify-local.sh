#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-fast}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run_fast() {
  (cd "$ROOT_DIR/functions" && npm run verify:fast)
  (cd "$ROOT_DIR/frontend" && npm run verify:fast)
}

run_browser() {
  run_fast
  bash "$ROOT_DIR/scripts/e2e-all.sh"
}

case "$MODE" in
  fast)
    run_fast
    ;;
  browser)
    run_browser
    ;;
  full)
    run_browser
    ;;
  *)
    echo "Usage: ./scripts/verify-local.sh [fast|browser|full]" >&2
    exit 1
    ;;
esac
