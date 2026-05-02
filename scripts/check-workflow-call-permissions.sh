#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(git -C "$SCRIPT_DIR/.." rev-parse --show-toplevel)"
cd "$REPO_ROOT"

status=0

rank_permission() {
  case "$1" in
    write | write-all)
      printf '2'
      ;;
    read | read-all)
      printf '1'
      ;;
    none | null | "" | "~")
      printf '0'
      ;;
    *)
      printf 'Unknown permission level: %s\n' "$1" >&2
      return 1
      ;;
  esac
}

caller_permission() {
  local caller="$1"
  local job_id="$2"
  local scope="$3"
  local job_permissions_tag
  local top_permissions_tag

  job_permissions_tag="$(JOB_ID="$job_id" yq eval -r '.jobs[strenv(JOB_ID)].permissions | tag' "$caller")"
  if [ "$job_permissions_tag" = "!!map" ]; then
    JOB_ID="$job_id" SCOPE="$scope" yq eval -r '.jobs[strenv(JOB_ID)].permissions[strenv(SCOPE)] // "none"' "$caller"
    return
  fi

  if [ "$job_permissions_tag" = "!!str" ]; then
    JOB_ID="$job_id" yq eval -r '.jobs[strenv(JOB_ID)].permissions' "$caller"
    return
  fi

  top_permissions_tag="$(yq eval -r '.permissions | tag' "$caller")"
  if [ "$top_permissions_tag" = "!!map" ]; then
    SCOPE="$scope" yq eval -r '.permissions[strenv(SCOPE)] // "none"' "$caller"
    return
  fi

  if [ "$top_permissions_tag" = "!!str" ]; then
    yq eval -r '.permissions' "$caller"
    return
  fi

  printf 'none'
}

workflow_files=("$@")
if [ "${#workflow_files[@]}" -eq 0 ]; then
  mapfile -t workflow_files < <(find .github/workflows -maxdepth 1 -type f \( -name '*.yml' -o -name '*.yaml' \) | sort)
fi

for caller in "${workflow_files[@]}"; do
  while IFS=$'\t' read -r job_id callee_path; do
    if [ -z "$job_id" ] && [ -z "$callee_path" ]; then
      continue
    fi

    callee="${callee_path#./}"
    if [ ! -f "$callee" ]; then
      printf 'Workflow call permission check failed: %s job %s calls missing workflow %s\n' \
        "$caller" "$job_id" "$callee_path" >&2
      status=1
      continue
    fi

    mapfile -t required_scopes < <(yq eval -r 'select(.permissions | tag == "!!map") | .permissions | keys | .[]' "$callee")
    for scope in "${required_scopes[@]}"; do
      required="$(SCOPE="$scope" yq eval -r '.permissions[strenv(SCOPE)] // "none"' "$callee")"
      allowed="$(caller_permission "$caller" "$job_id" "$scope")"
      required_rank="$(rank_permission "$required")"
      allowed_rank="$(rank_permission "$allowed")"

      if [ "$allowed_rank" -lt "$required_rank" ]; then
        printf 'Workflow call permission check failed: %s job %s calls %s, which requests %s: %s but caller allows %s: %s\n' \
          "$caller" "$job_id" "$callee_path" "$scope" "$required" "$scope" "$allowed" >&2
        status=1
      fi
    done
  done < <(yq eval -r '.jobs | to_entries[] | select(.value.uses != null and (.value.uses | test("^\\./\\.github/workflows/"))) | [.key, .value.uses] | @tsv' "$caller")
done

exit "$status"
