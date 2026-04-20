#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors
#
# Runs the three .NET test projects concurrently, captures each log, and
# returns non-zero if any fails. Designed for a single runner with shared
# build artifacts (uses --no-build), so no extra restore/build cost.
#
# Usage: scripts/run-tests-parallel.sh <results-dir>

set -uo pipefail

RESULTS_DIR="${1:-./artifacts/test-results}"
mkdir -p "$RESULTS_DIR"

run_test() {
  local project="$1"
  local name="$2"
  dotnet test "$project" -c Release --no-build \
    --logger "trx;LogFileName=${name}.trx" \
    --logger "console;verbosity=normal" \
    --results-directory "$RESULTS_DIR" \
    > "$RESULTS_DIR/${name}.log" 2>&1
}

run_test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj api-tests &
pid_api=$!
run_test tests/Lfm.App.Tests/Lfm.App.Tests.csproj app-tests &
pid_app=$!
run_test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj app-core-tests &
pid_core=$!

fail=0
wait "$pid_api" || fail=$?
wait "$pid_app" || fail=$?
wait "$pid_core" || fail=$?

print_group() {
  local title="$1"
  local file="$2"
  echo "::group::$title"
  [ -f "$file" ] && cat "$file"
  echo "::endgroup::"
}

print_group "API test output"       "$RESULTS_DIR/api-tests.log"
print_group "App test output"       "$RESULTS_DIR/app-tests.log"
print_group "App.Core test output"  "$RESULTS_DIR/app-core-tests.log"

exit "$fail"
