#!/usr/bin/env bash
# Run all E2E scenarios, each against the correct seed state.
# Each invocation of e2e.sh starts and tears down the Docker stack.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/scripts/e2e.sh"

# Default scenario — all non-scenario-specific specs in one Docker cycle.
"$RUNNER" default landing-page.spec.ts first-paint.spec.ts access-control.spec.ts login-entry.spec.ts raids.spec.ts a11y.spec.ts signup.spec.ts create-raid.spec.ts

# Scenario-specific specs — each requires its own seed state.
"$RUNNER" raids-empty raids-empty.spec.ts
"$RUNNER" raids-error raids-error.spec.ts
"$RUNNER" characters-empty characters-empty.spec.ts
"$RUNNER" instances-missing create-raid-instances-missing.spec.ts
