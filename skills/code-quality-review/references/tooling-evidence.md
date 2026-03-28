# Tooling Evidence For Sandbox-First Code Quality Reviews

This reference maps the approved BrowserStack-derived metrics to trustworthy evidence sources for repository-wide reviews.

Use it after `browserstack-metrics.md`.

## Execution Order

Use this tool provisioning order:

1. Docker Sandbox
2. Official or publisher-maintained Docker images
3. Local installation

Why:

- Docker Sandbox keeps the wider toolkit off the host while still allowing persistent installs inside the sandbox
- official images reduce trust risk for tools that already publish maintained container workflows
- local installation is the fallback when container execution is missing or impractical

Do not use floating tags such as `latest` or `master`.
Prefer pinned versions and first-party distribution channels.

## No-Tools Fallback

When Docker Sandbox and external tools are not available (common in sandboxed agentic environments like Claude Code):

1. Skip the bootstrap planner script entirely.
2. Use only repo-native commands as evidence sources:
   - Build/type-check commands from package manifests
   - Test commands and their output
   - Lint commands and their output
   - `git log` and `git blame` for churn and hotspot analysis
3. For direct metrics that require external tools (Cyclomatic Complexity, Code Security scanners, Code Coverage):
   - Rate as `not assessed` if repo-native equivalents do not exist
   - Note reduced confidence explicitly in findings
4. For derived and inspection-dominant metrics, proceed normally — these rely primarily on code reading and repo-native tool output.
5. State the tooling gap in the Residual Unknowns section of the report.

This fallback produces a valid review with reduced confidence on direct metrics. It does not reduce the rigor of Tier 1 assessment, which is primarily inspection-driven.

## Sandbox Bootstrap

Use the planner before choosing tools:

```bash
scripts/bootstrap-sandbox.sh --json --repo "$PWD"
```

The planner reports:

- the pinned base toolkit
- which language adapter packs the repository needs
- the fallback order when Docker Sandbox is unavailable

The planner is intentionally conservative. It tells you what to provision and review with; it does not auto-score the repository.

## Base Toolkit

Use these tools as the default cross-language evidence stack:

| Tool | Version | Strategy | Primary use |
| --- | --- | --- | --- |
| `git` | host | system package | churn, hotspot history, recent change patterns |
| `jq` | host | system package | JSON report processing |
| `ripgrep` | host | system package | TODO/FIXME clusters, targeted code search |
| `scc` | `v3.7.0` | release binary | language mix, LOC, ULOC/DRYness, coarse structure |
| `lizard` | `1.20.0` | `pip` | function-level cyclomatic complexity and hotspots |
| `semgrep` | `v1.156.0` | `pip` | code-level security and bug-pattern signals |
| `trivy` | `v0.69.3` | release binary | dependency, secret, and misconfiguration signals |
| `hyperfine` | `v1.20.0` | release binary | targeted command benchmarking only |
| `jscpd` | `v3.5.10` | `npm` | deeper duplication analysis when needed |

## Adapter Packs

The planner activates these packs based on repository manifests.

| Adapter | Trigger files | Tools |
| --- | --- | --- |
| `javascript-typescript` | `package.json` | repo-native `eslint`, `tsc`, test runner, LCOV/JUnit exports where available |
| `python` | `pyproject.toml`, `requirements.txt`, `setup.py`, `Pipfile` | `ruff`, `mypy`, `pytest`, `coverage.py` |
| `go` | `go.mod` | `go test`, `golangci-lint` |
| `rust` | `Cargo.toml` | `cargo test`, `cargo clippy`, `cargo llvm-cov` |
| `java-kotlin` | `pom.xml`, `build.gradle`, `build.gradle.kts`, `settings.gradle`, `settings.gradle.kts` | build-tool-native tests, JaCoCo, SpotBugs, Detekt |
| `dotnet` | `*.csproj`, `*.sln`, `global.json`, `Directory.Build.props` | `dotnet test`, `dotnet-coverage` |
| `ruby` | `Gemfile` | `rubocop`, `brakeman` |
| `php` | `composer.json` | `phpunit`, `phpstan` |

Use repo-native build, test, and coverage commands as the source of truth when the repository already defines them.
Do not replace working project-native commands with global tools just because the adapter exists.

## Metric Evidence Classes

### Direct metrics

These can be scored directly from tool output plus light inspection:

| Metric | Primary evidence |
| --- | --- |
| `Cyclomatic Complexity` | `lizard`, `scc` |
| `Code Security` | `semgrep`, `trivy`, targeted inspection of auth, validation, and secret handling |
| `Code Churn` | `git log`, `git blame`, hotspot overlap with complexity or failures |
| `Unit Test Results` | repo-native test command results, JUnit where available |
| `Code Coverage` | LCOV, Cobertura XML, `coverage.py` XML, repo-native coverage reports |
| `Efficiency` | `hyperfine` for stable commands, plus obvious hot-path waste from inspection |

### Derived metrics

These require combining tool output with repository inspection:

| Metric | Primary evidence |
| --- | --- |
| `Maintainability` | module boundaries, file size, duplication, complexity clusters, churn overlap |
| `Testability` | deterministic seams, test entry points, side-effect isolation, coverage support |
| `Reliability` | test results, bug-pattern signals, error handling, recovery behavior |
| `Readability` | naming, control-flow simplicity, long functions, nesting, intent clarity |
| `Technical Debt` | TODO/FIXME clusters, duplication, workaround layering, stale abstractions |
| `Reusability` | duplication trends, shared abstractions, coupling tradeoffs |

### Inspection-dominant metrics

These should remain review-driven even when tools provide supporting signals:

| Metric | Primary evidence |
| --- | --- |
| `Documentation` | setup accuracy, architecture notes, extension guidance, ownership notes |
| `Extensibility` | ability to add behavior without invasive edits, visible seams, interface design |
| `Portability` | CI/runtime assumptions, cross-platform path usage, deploy-target independence |

## Guardrails

- Tool output is evidence, not a numeric score.
- If trustworthy evidence is missing, use `not assessed`.
- Do not let coverage, churn, or unit pass rate dominate Tier 1 judgments.
- Do not treat security scanner output as a substitute for maintainability or architecture analysis.
- Use `hyperfine` only for stable, side-effect-free commands or existing performance checks.
- Keep test and coverage commands host-native unless the repository already exposes a containerized verification entry point.

## Trust Gate

Only recommend tools that meet all of these:

- active maintenance
- current stable release line
- official docs or maintained project page
- Linux, macOS, Windows, or WSL viability
- local CLI usage

Reference sources used to build this stack:

- Docker Sandboxes: <https://docs.docker.com/ai/sandboxes/>
- Docker Sandbox architecture: <https://docs.docker.com/ai/sandboxes/architecture/>
- Lizard: <https://github.com/terryyin/lizard/releases/tag/1.20.0>
- SCC: <https://github.com/boyter/scc/releases/tag/v3.7.0>
- Semgrep: <https://github.com/semgrep/semgrep/releases/tag/v1.156.0>
- Trivy: <https://github.com/aquasecurity/trivy/releases/tag/v0.69.3>
- Hyperfine: <https://github.com/sharkdp/hyperfine/releases/tag/v1.20.0>
- JSCPD: <https://github.com/kucherenko/jscpd>
- Ruff: <https://docs.astral.sh/ruff/>
- Mypy: <https://mypy.readthedocs.io/>
- GolangCI-Lint: <https://golangci-lint.run/>
- Detekt: <https://github.com/detekt/detekt/releases/tag/v1.23.8>
- JaCoCo: <https://www.jacoco.org/jacoco/trunk/doc/>
- SpotBugs: <https://spotbugs.readthedocs.io/>
- dotnet-coverage: <https://learn.microsoft.com/dotnet/core/additional-tools/dotnet-coverage>
- RuboCop: <https://docs.rubocop.org/rubocop/>
- Brakeman: <https://brakemanscanner.org/docs/>
- PHPStan: <https://phpstan.org/user-guide/getting-started>
- PHPUnit: <https://docs.phpunit.de/>
