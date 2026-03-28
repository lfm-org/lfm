#!/usr/bin/env bash
set -euo pipefail

MODE="summary"
REPO_DIR="."

usage() {
  cat <<'EOF'
Usage: bootstrap-sandbox.sh [--json] [--repo <path>]

Plans the Docker Sandbox toolkit for a repository by listing the pinned base
toolkit and any language adapter packs detected from repository manifests.
EOF
}

json_escape() {
  local value="$1"
  value=${value//\\/\\\\}
  value=${value//\"/\\\"}
  value=${value//$'\n'/\\n}
  value=${value//$'\r'/\\r}
  value=${value//$'\t'/\\t}
  printf '%s' "$value"
}

detect_reasons() {
  local repo_dir="$1"
  shift
  local reasons=()
  local marker
  local match_path

  for marker in "$@"; do
    match_path="$(
      find "$repo_dir" \
      \( -path '*/.git/*' -o -path '*/node_modules/*' -o -path '*/vendor/*' -o -path '*/dist/*' -o -path '*/build/*' \) -prune \
      -o -type f -name "$marker" -print -quit
    )"

    if [[ -n "$match_path" ]]; then
      reasons+=("$(basename "$match_path")")
    fi
  done

  printf '%s\n' "${reasons[@]}"
}

append_adapter_if_detected() {
  local adapter_id="$1"
  shift
  local reasons

  mapfile -t reasons < <(detect_reasons "$REPO_DIR" "$@" | sed '/^$/d')
  if [[ ${#reasons[@]} -eq 0 ]]; then
    return
  fi

  ADAPTER_IDS+=("$adapter_id")
  local joined=""
  local reason
  for reason in "${reasons[@]}"; do
    if [[ -n "$joined" ]]; then
      joined="$joined|$reason"
    else
      joined="$reason"
    fi
  done
  ADAPTER_REASONS["$adapter_id"]="$joined"
}

print_json_array_of_strings() {
  local items=("$@")
  local index

  printf '['
  for index in "${!items[@]}"; do
    if [[ "$index" -gt 0 ]]; then
      printf ','
    fi
    printf '"%s"' "$(json_escape "${items[$index]}")"
  done
  printf ']'
}

print_base_tools_json() {
  local tools=("${BASE_TOOL_IDS[@]}")
  local index
  local tool

  printf '['
  for index in "${!tools[@]}"; do
    tool="${tools[$index]}"
    if [[ "$index" -gt 0 ]]; then
      printf ','
    fi
    printf '{'
    printf '"id":"%s",' "$(json_escape "$tool")"
    printf '"version":"%s",' "$(json_escape "${BASE_TOOL_VERSION[$tool]}")"
    printf '"strategy":"%s",' "$(json_escape "${BASE_TOOL_STRATEGY[$tool]}")"
    printf '"source":"%s"' "$(json_escape "${BASE_TOOL_SOURCE[$tool]}")"
    printf '}'
  done
  printf ']'
}

adapter_tools_for() {
  case "$1" in
    javascript-typescript)
      printf '%s\n' "node-runtime" "repo-native-eslint" "repo-native-tsc" "repo-native-test-runner" "repo-native-coverage-export"
      ;;
    python)
      printf '%s\n' "ruff" "mypy" "pytest" "coverage.py"
      ;;
    go)
      printf '%s\n' "go-test" "golangci-lint"
      ;;
    rust)
      printf '%s\n' "cargo-test" "cargo-clippy" "cargo-llvm-cov"
      ;;
    java-kotlin)
      printf '%s\n' "build-tool-native-tests" "jacoco" "spotbugs" "detekt"
      ;;
    dotnet)
      printf '%s\n' "dotnet-test" "dotnet-coverage"
      ;;
    ruby)
      printf '%s\n' "rubocop" "brakeman"
      ;;
    php)
      printf '%s\n' "phpunit" "phpstan"
      ;;
    *)
      return 1
      ;;
  esac
}

print_adapters_json() {
  local index
  local adapter_id
  local tools
  local reasons

  printf '['
  for index in "${!ADAPTER_IDS[@]}"; do
    adapter_id="${ADAPTER_IDS[$index]}"
    IFS='|' read -ra reasons <<< "${ADAPTER_REASONS[$adapter_id]}"
    mapfile -t tools < <(adapter_tools_for "$adapter_id")

    if [[ "$index" -gt 0 ]]; then
      printf ','
    fi

    printf '{'
    printf '"id":"%s",' "$(json_escape "$adapter_id")"
    printf '"reasons":'
    print_json_array_of_strings "${reasons[@]}"
    printf ',"tools":'
    print_json_array_of_strings "${tools[@]}"
    printf '}'
  done
  printf ']'
}

print_summary() {
  local tool
  local adapter_id
  local reasons

  printf 'repo: %s\n' "$REPO_DIR"
  printf 'fallback-order: docker-sandbox -> docker-images -> local-install\n'
  printf 'base-tools:\n'
  for tool in "${BASE_TOOL_IDS[@]}"; do
    printf '  - %s (%s, %s)\n' "$tool" "${BASE_TOOL_VERSION[$tool]}" "${BASE_TOOL_STRATEGY[$tool]}"
  done

  if [[ ${#ADAPTER_IDS[@]} -eq 0 ]]; then
    printf 'adapters: none\n'
    return
  fi

  printf 'adapters:\n'
  for adapter_id in "${ADAPTER_IDS[@]}"; do
    IFS='|' read -ra reasons <<< "${ADAPTER_REASONS[$adapter_id]}"
    printf '  - %s (%s)\n' "$adapter_id" "$(IFS=', '; echo "${reasons[*]}")"
  done
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --json)
      MODE="json"
      shift
      ;;
    --repo)
      if [[ $# -lt 2 ]]; then
        echo "missing value for --repo" >&2
        exit 1
      fi
      REPO_DIR="$2"
      shift 2
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      echo "unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

REPO_DIR="$(cd "$REPO_DIR" && pwd)"

declare -a BASE_TOOL_IDS=(
  "git"
  "jq"
  "ripgrep"
  "scc"
  "lizard"
  "semgrep"
  "trivy"
  "hyperfine"
  "jscpd"
)

declare -A BASE_TOOL_VERSION=(
  ["git"]="host"
  ["jq"]="host"
  ["ripgrep"]="host"
  ["scc"]="v3.7.0"
  ["lizard"]="1.20.0"
  ["semgrep"]="v1.156.0"
  ["trivy"]="v0.69.3"
  ["hyperfine"]="v1.20.0"
  ["jscpd"]="v3.5.10"
)

declare -A BASE_TOOL_STRATEGY=(
  ["git"]="system-package"
  ["jq"]="system-package"
  ["ripgrep"]="system-package"
  ["scc"]="release-binary"
  ["lizard"]="pip-user"
  ["semgrep"]="pip-user"
  ["trivy"]="release-binary"
  ["hyperfine"]="release-binary"
  ["jscpd"]="npm-global"
)

declare -A BASE_TOOL_SOURCE=(
  ["git"]="https://git-scm.com/"
  ["jq"]="https://jqlang.org/"
  ["ripgrep"]="https://github.com/BurntSushi/ripgrep"
  ["scc"]="https://github.com/boyter/scc/releases/tag/v3.7.0"
  ["lizard"]="https://github.com/terryyin/lizard/releases/tag/1.20.0"
  ["semgrep"]="https://github.com/semgrep/semgrep/releases/tag/v1.156.0"
  ["trivy"]="https://github.com/aquasecurity/trivy/releases/tag/v0.69.3"
  ["hyperfine"]="https://github.com/sharkdp/hyperfine/releases/tag/v1.20.0"
  ["jscpd"]="https://github.com/kucherenko/jscpd"
)

declare -a ADAPTER_IDS=()
declare -A ADAPTER_REASONS=()

append_adapter_if_detected "javascript-typescript" "package.json"
append_adapter_if_detected "python" "pyproject.toml" "requirements.txt" "setup.py" "Pipfile"
append_adapter_if_detected "go" "go.mod"
append_adapter_if_detected "rust" "Cargo.toml"
append_adapter_if_detected "java-kotlin" "pom.xml" "build.gradle" "build.gradle.kts" "settings.gradle" "settings.gradle.kts"
append_adapter_if_detected "dotnet" "*.csproj" "*.sln" "global.json" "Directory.Build.props"
append_adapter_if_detected "ruby" "Gemfile"
append_adapter_if_detected "php" "composer.json"

if [[ "$MODE" == "json" ]]; then
  printf '{'
  printf '"repo":"%s",' "$(json_escape "$REPO_DIR")"
  printf '"fallbackOrder":["docker-sandbox","docker-images","local-install"],'
  printf '"baseTools":'
  print_base_tools_json
  printf ',"adapters":'
  print_adapters_json
  printf '}\n'
else
  print_summary
fi
