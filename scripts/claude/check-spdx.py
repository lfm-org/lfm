#!/usr/bin/env -S uv run --script

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Verify that every file in a list contains `SPDX-License-Identifier` in its head.

Usage:
    check-spdx.py <file-list> [--root <repo-root>] [--head-lines N]

`<file-list>` is a path to a text file containing one relative path per line
(as produced by `git ls-files`). Paths are resolved against `--root` (default
`.`).

Checks the first N lines (default 10) for `SPDX-License-Identifier`. This is
more robust than `head -5 | grep` because some files have a shebang plus a
blank line plus the SPDX header, which pushes the identifier to line 3+.

Exit codes:
    0 — every listed file has the identifier
    1 — one or more files missing; missing files are printed to stdout
    2 — usage error

Used by the AGPL licensing plan at
`docs/superpowers/plans/2026-04-16-agpl-licensing.md` (Phase 2 tasks).

Invocation requires `UV_CACHE_DIR` pointing at a sandbox-writable path:

    export UV_CACHE_DIR=/tmp/claude/uv-cache
    mkdir -p "$UV_CACHE_DIR"
    ./scripts/claude/check-spdx.py /tmp/claude/phase-2b-files.txt --root /path/to/repo
"""

from __future__ import annotations

import sys
from argparse import ArgumentParser
from pathlib import Path


def main(argv: list[str]) -> int:
    ap = ArgumentParser()
    ap.add_argument("file_list")
    ap.add_argument("--root", default=".")
    ap.add_argument("--head-lines", type=int, default=10)
    args = ap.parse_args(argv[1:])

    root = Path(args.root).resolve()
    list_path = Path(args.file_list)
    if not list_path.is_file():
        print(f"not a file: {list_path}", file=sys.stderr)
        return 2

    missing: list[str] = []
    total = 0
    for raw in list_path.read_text(encoding="utf-8").splitlines():
        rel = raw.strip()
        if not rel:
            continue
        total += 1
        target = root / rel
        try:
            with target.open("r", encoding="utf-8", errors="replace") as f:
                head = []
                for _ in range(args.head_lines):
                    line = f.readline()
                    if not line:
                        break
                    head.append(line)
        except OSError as e:
            print(f"MISSING: {rel} (open error: {e})")
            missing.append(rel)
            continue

        if not any("SPDX-License-Identifier" in line for line in head):
            print(f"MISSING: {rel}")
            missing.append(rel)

    print(f"missing: {len(missing)} / total: {total}", file=sys.stderr)
    return 0 if not missing else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
