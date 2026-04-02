#!/usr/bin/env bash
# Run all E2E scenarios, each against the correct seed state.
# Each invocation of e2e.sh starts and tears down the Docker stack.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/scripts/e2e.sh"

# Default scenario — run everything discovered by Playwright that does not
# require a non-default E2E_SCENARIO.
"$RUNNER" default

# Scenario-specific specs — each requires its own seed state.
"$RUNNER" runs-empty e2e/runs-empty.spec.ts
"$RUNNER" runs-error e2e/runs-error.spec.ts
"$RUNNER" characters-empty e2e/characters-empty.spec.ts
"$RUNNER" instances-missing e2e/create-run-instances-missing.spec.ts
