#!/usr/bin/env -S uv run --script

# SPDX-License-Identifier: AGPL-3.0-or-later
# SPDX-FileCopyrightText: 2026 LFM contributors

# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Prepend SPDX-License-Identifier + SPDX-FileCopyrightText headers to a file.

Usage:
    add-spdx.py <path>

Skips files that already contain `SPDX-License-Identifier`.
Preserves shebang on line 1 when present.
Comment syntax chosen by extension:
  - `//` for .cs, .bicep
  - `#`  for .sh, .ps1, .yml, .yaml
Other extensions exit with status 2 (unsupported).

Used by the AGPL licensing plan at
`docs/superpowers/plans/2026-04-16-agpl-licensing.md` (Phase 2 tasks).

Invocation requires `UV_CACHE_DIR` pointing at a sandbox-writable path, since
the default `~/.cache/uv` is blocked by the nono sandbox:

    export UV_CACHE_DIR=/tmp/claude/uv-cache
    mkdir -p "$UV_CACHE_DIR"
    ./scripts/claude/add-spdx.py <file>
"""

from __future__ import annotations

import sys
from pathlib import Path

LICENSE_ID = "AGPL-3.0-or-later"
COPYRIGHT = "2026 LFM contributors"

EXT_PREFIX: dict[str, str] = {
    ".cs": "//",
    ".bicep": "//",
    ".sh": "#",
    ".ps1": "#",
    ".yml": "#",
    ".yaml": "#",
}


def build_header(prefix: str) -> str:
    # REUSE-IgnoreStart
    return (
        f"{prefix} SPDX-License-Identifier: {LICENSE_ID}\n"
        f"{prefix} SPDX-FileCopyrightText: {COPYRIGHT}\n"
        f"\n"
    )
    # REUSE-IgnoreEnd


def transform(text: str, header: str) -> str:
    # Preserve shebang on line 1 if present.
    if text.startswith("#!"):
        first_nl = text.find("\n")
        if first_nl == -1:
            return text + "\n\n" + header
        shebang = text[: first_nl + 1]
        rest = text[first_nl + 1 :]
        return f"{shebang}\n{header}{rest}"
    return header + text


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print("usage: add-spdx.py <path>", file=sys.stderr)
        return 2

    path = Path(argv[1])
    if not path.is_file():
        print(f"not a file: {path}", file=sys.stderr)
        return 1

    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as e:
        print(f"not UTF-8: {path}: {e}", file=sys.stderr)
        return 1

    if "SPDX-License-Identifier" in text:
        return 0

    prefix = EXT_PREFIX.get(path.suffix.lower())
    if prefix is None:
        print(f"unsupported extension: {path.suffix} ({path})", file=sys.stderr)
        return 2

    new_text = transform(text, build_header(prefix))
    path.write_text(new_text, encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
