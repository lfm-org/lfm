#!/usr/bin/env bash

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_dir="$repo_root/skills"
codex_home="${CODEX_HOME:-$HOME/.codex}"
target_dir="$codex_home/skills"

mkdir -p "$target_dir"

installed=0

for skill_dir in "$source_dir"/*; do
  if [[ ! -d "$skill_dir" ]]; then
    continue
  fi

  skill_name="$(basename "$skill_dir")"
  target_path="$target_dir/$skill_name"

  if [[ -L "$target_path" ]]; then
    existing_target="$(readlink "$target_path")"
    if [[ "$existing_target" == "$skill_dir" ]]; then
      echo "already linked $skill_name"
      installed=1
      continue
    fi
    rm "$target_path"
  elif [[ -e "$target_path" ]]; then
    echo "refusing to replace existing non-symlink path: $target_path" >&2
    exit 1
  fi

  ln -s "$skill_dir" "$target_path"
  echo "linked $skill_name -> $target_path"
  installed=1
done

if [[ "$installed" -eq 0 ]]; then
  echo "no repo skills found under $source_dir" >&2
  exit 1
fi

echo "restart Codex to pick up installed skills"
