#!/usr/bin/env bash
set -euo pipefail

MODE="${1:-fast}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

run_fast() {
  fnm exec pnpm -C "$ROOT_DIR/functions" verify:fast
  fnm exec pnpm -C "$ROOT_DIR/frontend" verify:fast
}

run_browser() {
  run_fast
  bash "$ROOT_DIR/scripts/e2e-all.sh"
}

run_perf() {
  PLAYWRIGHT_INCLUDE_PERF_SPECS=1 bash "$ROOT_DIR/scripts/e2e.sh" default \
    e2e/perf/async-actions.perf.spec.ts \
    e2e/perf/forms.perf.spec.ts \
    e2e/perf/load.perf.spec.ts \
    e2e/perf/mobile.perf.spec.ts \
    e2e/perf/navigation.perf.spec.ts
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
    run_perf
    ;;
  *)
    echo "Usage: ./scripts/verify-local.sh [fast|browser|full]" >&2
    exit 1
    ;;
esac
