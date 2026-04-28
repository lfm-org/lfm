#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 LFM contributors
# SPDX-License-Identifier: AGPL-3.0-or-later
#
# Render every view in an ArchiMate OEF XML file as a PNG using Archi's
# headless CLI (HTML report mode — the only Archi CLI path that produces
# view images as a side-effect).
#
# Safe to run concurrently: each run gets its own Archi workspace
# (-configuration, -data) and HTML report dir under the configured cache root.
# Output PNGs are scoped by OEF filename stem, so different OEFs never collide;
# concurrent renders of the same OEF settle to last-writer-wins per-file without
# racing each other mid-copy.

set -euo pipefail
IFS=$'\n\t'

usage() {
  cat <<'EOF'
Usage: scripts/archi-render.sh [OPTIONS] [OEF_FILE]

Render every view in an ArchiMate OEF XML file as a PNG via Archi's CLI.

Output goes to .cache/archi-views/<stem>/ by default, where <stem> is the OEF
filename with .oef.xml / .xml stripped. Concurrent-safe: runs from different
agents use isolated workspaces.

Options:
      --archi-bin PATH    Path to Archi executable.
      --cache-root DIR    Root for transient Archi workdirs and failure logs.
      --config FILE       Read KEY=VALUE settings. Config values override CLI
                          arguments and environment values.
      --output-root DIR   Root for rendered PNG output; <stem>/ is appended.
  -q, --quiet             Suppress Archi progress output. Only print result paths.
  -h, --help              Show this help.

Arguments:
  OEF_FILE       Path to an OEF XML file. Default: docs/architecture/lfm.oef.xml
                 Relative paths resolve against the repository root.

Environment:
  ARCHI_BIN                 Path to Archi executable.
                            Default: $HOME/.local/bin/Archi.
  ARCHI_RENDER_CACHE_ROOT   Root for transient workdirs and failure logs.
                            Default: .cache/archi.
  ARCHI_RENDER_CONFIG       Config file path. Relative paths resolve against the
                            repository root.
  ARCHI_RENDER_OEF_FILE     Default OEF file when OEF_FILE is omitted.
  ARCHI_RENDER_OUTPUT_ROOT  Root for rendered PNG output.
                            Default: .cache/archi-views.
  DISPLAY        Required. Archi's SWT needs an X display (use xvfb-run on
                 pure Wayland without Xwayland).

Config file keys:
  ARCHI_BIN, ARCHI_RENDER_CACHE_ROOT, ARCHI_RENDER_OEF_FILE,
  ARCHI_RENDER_OUTPUT_ROOT

Precedence:
  config file > CLI argument > environment variable > default

Exit codes:
  0  success — PNGs written to the configured output root under <stem>/
  1  usage error (missing Archi / OEF / DISPLAY / git / xmllint / mktemp /
     realpath)
  2  OEF file is not well-formed XML
  3  Archi CLI returned non-zero (see stderr for log path)
  4  Archi completed but produced no view images
EOF
}

die() {
  echo "archi-render: $*" >&2
  exit 1
}

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "$value"
}

strip_optional_quotes() {
  local value="$1"
  if [[ ${#value} -ge 2 ]]; then
    case "$value" in
      \"*\") value="${value:1:${#value}-2}" ;;
      \'*\') value="${value:1:${#value}-2}" ;;
    esac
  fi
  printf '%s' "$value"
}

expand_home() {
  local path="$1"
  case "$path" in
    \~) printf '%s' "$HOME" ;;
    \~/*) printf '%s/%s' "$HOME" "${path#\~/}" ;;
    *) printf '%s' "$path" ;;
  esac
}

resolve_repo_path() {
  local path
  path="$(expand_home "$1")"
  if [[ "$path" = /* ]]; then
    printf '%s' "$path"
  else
    printf '%s/%s' "$repo_root" "$path"
  fi
}

load_config() {
  local config="$1"
  local line line_no key value

  [[ -f "$config" ]] || die "config file not found: $config"

  line_no=0
  while IFS= read -r line || [[ -n "$line" ]]; do
    line_no=$((line_no + 1))
    line="$(trim "$line")"
    [[ -z "$line" || "${line:0:1}" = "#" ]] && continue

    if [[ "$line" = export[[:space:]]* ]]; then
      line="$(trim "${line#export}")"
    fi

    [[ "$line" = *=* ]] || die "invalid config line $line_no in $config (expected KEY=VALUE)"
    key="$(trim "${line%%=*}")"
    value="$(strip_optional_quotes "$(trim "${line#*=}")")"

    case "$key" in
      ARCHI_BIN) archi_bin="$value" ;;
      ARCHI_RENDER_CACHE_ROOT) cache_root="$value" ;;
      ARCHI_RENDER_OEF_FILE) oef_rel="$value" ;;
      ARCHI_RENDER_OUTPUT_ROOT) output_root="$value" ;;
      *) die "unknown config key $key in $config" ;;
    esac
  done < "$config"
}

# ---- argparse -----------------------------------------------------------

quiet=0
config_file="${ARCHI_RENDER_CONFIG:-}"
cli_archi_bin=""
cli_cache_root=""
cli_output_root=""
oef_arg=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    -h|--help) usage; exit 0 ;;
    -q|--quiet) quiet=1 ;;
    --archi-bin)
      shift
      [[ $# -gt 0 ]] || die "--archi-bin requires a path"
      cli_archi_bin="$1"
      ;;
    --archi-bin=*) cli_archi_bin="${1#*=}" ;;
    --cache-root)
      shift
      [[ $# -gt 0 ]] || die "--cache-root requires a directory"
      cli_cache_root="$1"
      ;;
    --cache-root=*) cli_cache_root="${1#*=}" ;;
    --config)
      shift
      [[ $# -gt 0 ]] || die "--config requires a file"
      config_file="$1"
      ;;
    --config=*) config_file="${1#*=}" ;;
    --output-root)
      shift
      [[ $# -gt 0 ]] || die "--output-root requires a directory"
      cli_output_root="$1"
      ;;
    --output-root=*) cli_output_root="${1#*=}" ;;
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
command -v realpath >/dev/null 2>&1 || die "realpath not in PATH"

# ---- path resolution ----------------------------------------------------

script="$(realpath "$0")"
script_dir="$(dirname "$script")"

repo_root="$(git -C "$script_dir" rev-parse --show-toplevel 2>/dev/null)" \
  || die "$script_dir is not inside a git working tree"

archi_bin="${ARCHI_BIN:-$HOME/.local/bin/Archi}"
cache_root="${ARCHI_RENDER_CACHE_ROOT:-.cache/archi}"
oef_rel="${ARCHI_RENDER_OEF_FILE:-docs/architecture/lfm.oef.xml}"
output_root="${ARCHI_RENDER_OUTPUT_ROOT:-.cache/archi-views}"

[[ -n "$cli_archi_bin" ]] && archi_bin="$cli_archi_bin"
[[ -n "$cli_cache_root" ]] && cache_root="$cli_cache_root"
[[ -n "$cli_output_root" ]] && output_root="$cli_output_root"
[[ -n "$oef_arg" ]] && oef_rel="$oef_arg"

if [[ -n "$config_file" ]]; then
  config_file="$(resolve_repo_path "$config_file")"
  load_config "$config_file"
fi

[[ -n "$archi_bin" ]] || die "Archi binary path cannot be empty"
[[ -n "$cache_root" ]] || die "cache root cannot be empty"
[[ -n "$oef_rel" ]] || die "OEF file path cannot be empty"
[[ -n "$output_root" ]] || die "output root cannot be empty"

archi_bin="$(resolve_repo_path "$archi_bin")"
cache_root="$(resolve_repo_path "$cache_root")"
output_root="$(resolve_repo_path "$output_root")"

if [[ "$oef_rel" = /* ]]; then
  oef_abs="$oef_rel"
else
  oef_abs="$repo_root/$oef_rel"
fi

[[ -f "$oef_abs" ]] || die "OEF file not found: $oef_abs"
oef_abs="$(realpath "$oef_abs")"

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
out="$output_root/$oef_stem"
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
  set +e
  "${archi_cmd[@]}" 2>&1 \
    | tee "$log" \
    | grep --line-buffered -E '\[(HTMLReport|XML Exchange)\]'
  rc=${PIPESTATUS[0]}
  set -e
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
