#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RUNNER="$ROOT_DIR/scripts/e2e.sh"

"$RUNNER" default landing-page.spec.ts first-paint.spec.ts access-control.spec.ts login-entry.spec.ts
"$RUNNER" default raids.spec.ts a11y.spec.ts
"$RUNNER" default signup.spec.ts
"$RUNNER" default create-raid.spec.ts
"$RUNNER" raids-empty raids-empty.spec.ts
"$RUNNER" raids-error raids-error.spec.ts
"$RUNNER" characters-empty characters-empty.spec.ts
"$RUNNER" instances-missing create-raid-instances-missing.spec.ts
