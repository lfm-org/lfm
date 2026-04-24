#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 LFM contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Render every view in an ArchiMate OEF XML file as a PNG using Archi's
# headless CLI (HTML report mode — the only Archi CLI path that produces
# view images as a side-effect).
#
# Safe to run concurrently: each run gets its own Archi workspace
# (-configuration, -data) and HTML report dir under .cache/archi/runs/.
# Output PNGs are scoped by OEF filename stem, so different OEFs never
# collide; concurrent renders of the same OEF settle to last-writer-wins
# per-file without racing each other mid-copy.

set -euo pipefail
IFS=$'\n\t'

usage() {
  cat <<'EOF'
Usage: scripts/archi-render.sh [-q] [-h] [OEF_FILE]

Render every view in an ArchiMate OEF XML file as a PNG via Archi's CLI.

Output goes to .cache/archi-views/<stem>/ (gitignored), where <stem> is the
OEF filename with .oef.xml / .xml stripped. Concurrent-safe: runs from
different agents use isolated workspaces.

Options:
  -q, --quiet    Suppress Archi progress output. Only print result paths.
  -h, --help     Show this help.

Arguments:
  OEF_FILE       Path to an OEF XML file. Default: docs/architecture/lfm.oef.xml
                 Relative paths resolve against the repository root.

Environment:
  ARCHI_BIN      Path to Archi executable (default: $HOME/.local/bin/Archi).
  DISPLAY        Required. Archi's SWT needs an X display (use xvfb-run on
                 pure Wayland without Xwayland).

Exit codes:
  0  success — PNGs written to .cache/archi-views/<stem>/
  1  usage error (missing Archi / OEF / DISPLAY / git / xmllint / mktemp)
  2  OEF file is not well-formed XML
  3  Archi CLI returned non-zero (see stderr for log path)
  4  Archi completed but produced no view images
EOF
}

die() {
  echo "archi-render: $*" >&2
  exit 1
}

# ---- argparse -----------------------------------------------------------

quiet=0
oef_arg=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    -q|--quiet) quiet=1 ;;
    --) shift; oef_arg="${1:-}"; break ;;
    -*) die "unknown option: $1 (try --help)" ;;
    *) oef_arg="$1" ;;
  esac
  shift
done

# ---- tool prerequisites -------------------------------------------------

command -v git     >/dev/null 2>&1 || die "git not in PATH"
command -v xmllint >/dev/null 2>&1 || die "xmllint not in PATH (install libxml2)"
command -v mktemp  >/dev/null 2>&1 || die "mktemp not in PATH"

# ---- path resolution ----------------------------------------------------

script="$(readlink -f "$0")"
script_dir="$(dirname "$script")"

repo_root="$(git -C "$script_dir" rev-parse --show-toplevel 2>/dev/null)" \
  || die "$script_dir is not inside a git working tree"

oef_rel="${oef_arg:-docs/architecture/lfm.oef.xml}"
if [[ "$oef_rel" = /* ]]; then
  oef_abs="$oef_rel"
else
  oef_abs="$repo_root/$oef_rel"
fi

[[ -f "$oef_abs" ]] || die "OEF file not found: $oef_abs"
oef_abs="$(realpath "$oef_abs")"

archi_bin="${ARCHI_BIN:-$HOME/.local/bin/Archi}"
[[ -x "$archi_bin" ]] || die "Archi binary not executable at $archi_bin (set ARCHI_BIN to override)"

[[ -n "${DISPLAY:-}" ]] || die "no \$DISPLAY set — Archi's SWT needs one (try: xvfb-run scripts/archi-render.sh)"

# ---- OEF well-formedness fast-fail --------------------------------------
# Catches malformed XML before the slow Archi JVM starts.

if ! xmllint_out="$(xmllint --noout "$oef_abs" 2>&1)"; then
  echo "archi-render: OEF file is not well-formed XML: $oef_abs" >&2
  printf '  %s\n' "$xmllint_out" >&2
  exit 2
fi

# ---- per-run workdir ---------------------------------------------------
# Isolated so concurrent agents don't clobber each other's Archi workspace,
# HTML report, or log. Cleaned up on EXIT regardless of success.

cache_root="$repo_root/.cache/archi"
runs_root="$cache_root/runs"
mkdir -p "$runs_root"

work="$(mktemp -d "$runs_root/run.XXXXXXXX")" \
  || die "could not create per-run workdir under $runs_root"
log="$work/archi.log"

cleanup() { rm -rf "$work"; }
trap cleanup EXIT
trap 'cleanup; exit 130' INT TERM

mkdir -p "$work/config" "$work/data" "$work/report"

# ---- output dir scoped by OEF stem -------------------------------------
# Different OEFs go to different subdirs → no inter-OEF collisions.
# Same OEF: concurrent runs settle to last-writer-wins per-file.

oef_base="$(basename "$oef_abs")"
oef_stem="${oef_base%.xml}"
oef_stem="${oef_stem%.oef}"
out="$repo_root/.cache/archi-views/$oef_stem"
mkdir -p "$out"
[[ -w "$out" ]] || die "output dir not writable: $out"

# ---- invoke Archi -------------------------------------------------------

archi_cmd=(
  "$archi_bin" -nosplash
  -application com.archimatetool.commandline.app
  -consoleLog
  -configuration "$work/config"
  -data          "$work/data"
  --abortOnException
  --xmlexchange.import "$oef_abs"
  --html.createReport  "$work/report"
)

rc=0
if [[ "$quiet" = 1 ]]; then
  "${archi_cmd[@]}" >"$log" 2>&1 || rc=$?
else
  "${archi_cmd[@]}" 2>&1 \
    | tee "$log" \
    | grep --line-buffered -E '\[(HTMLReport|XML Exchange)\]' || true
  rc=${PIPESTATUS[0]}
fi

if [[ $rc -ne 0 ]]; then
  # Copy the log out of the workdir before the EXIT trap wipes it.
  persist_log="$cache_root/last-failure-$oef_stem.log"
  cp -- "$log" "$persist_log" 2>/dev/null || persist_log="$log (deleted on exit)"
  echo "archi-render: Archi exited $rc — log preserved at $persist_log" >&2
  exit 3
fi

# ---- collect PNGs -------------------------------------------------------

png_src="$(find "$work/report" -type d -name images -print -quit)"
[[ -n "$png_src" ]] || {
  persist_log="$cache_root/last-failure-$oef_stem.log"
  cp -- "$log" "$persist_log" 2>/dev/null || persist_log="$log (deleted on exit)"
  echo "archi-render: Archi produced no images/ directory under $work/report" >&2
  echo "              log preserved at $persist_log" >&2
  exit 4
}

shopt -s nullglob
pngs=("$png_src"/*.png)
shopt -u nullglob
(( ${#pngs[@]} > 0 )) || {
  persist_log="$cache_root/last-failure-$oef_stem.log"
  cp -- "$log" "$persist_log" 2>/dev/null || persist_log="$log (deleted on exit)"
  echo "archi-render: $png_src exists but contains no PNGs" >&2
  echo "              log preserved at $persist_log" >&2
  exit 4
}

# Per-file cp -f — atomic per file on POSIX, no pre-wipe, so concurrent
# same-OEF runs end up with a consistent set where each PNG is from exactly
# one writer (the last one to finish copying that specific file).
cp -f -- "${pngs[@]}" "$out/"

# ---- report -------------------------------------------------------------

if [[ "$quiet" = 0 ]]; then
  echo
  echo "archi-render: wrote ${#pngs[@]} view PNGs to $out/"
fi
printf '%s\n' "$out"/*.png
