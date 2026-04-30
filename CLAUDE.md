# Repository Guidelines

- `app/` — Blazor WASM SPA → Static Web Apps
- `app/Lfm.App.Core/` — framework-neutral services, i18n, auth extracted from `app/` so Stryker can mutate them
- `api/` — Azure Functions backend (.NET 10 isolated)
- `shared/` — shared models and contracts (C#)
- `tests/` — xUnit test projects (unit, bUnit, E2E)
- `infra/` — Bicep IaC templates

When working on Azure-related tasks, use the `microsoft-docs` skill to look up official documentation before making changes.

## Cost Guidance

Hobby project. Prefer free tiers: Cosmos DB free tier, Functions Flex Consumption (FC1, on-demand with free grants; Linux Consumption Y1 is being retired Sept 2028), Static Web Apps Free, workspace-based App Insights sharing Log Analytics 5 GB/month free ingestion. Small fixed costs (Key Vault ops, Storage LRS) are fine. Avoid significant recurring costs without discussing first.

## Mandatory Git Workflow

1. **Start clean:** Run `git -C <repo-root> status --short --branch` before edits. If the workspace is dirty, stop and alert.
2. **Git path:** Always run git commands as `git -C <repo-root>` with an absolute path.
3. **Branch:** Never implement on `main`/`master`. Before edits, work in `agents/<short-slug>` via `git switch -c`.
4. **Worktrees:** Use an isolated worktree for implementation tasks. Worktrees live in repo-root `.worktrees/`; use `superpowers:using-git-worktrees` when available, otherwise `git worktree add .worktrees/<slug>`. Never create worktrees as sibling directories in `~/repos/`.
5. **Size:** Keep changesets small: commits ≤ 5 files / ≤ 250 lines; branches ≤ 30 files / ≤ 900 lines vs `main`. Thresholds guide planning, not design; commit partial finishes and split into subtasks before exceeding them.
6. **Verify:** Before claiming complete, use `superpowers:verification-before-completion` and run the relevant Verification commands below. Documentation-only changes that cannot affect build output may use targeted documentation verification; state when the full build is intentionally skipped.
7. **Review:** Non-trivial tasks require `superpowers:requesting-code-review` before merging.
8. **Merge:** Merge strategy is rebase-and-merge. Use `superpowers:finishing-a-development-branch` to close a branch.
9. **Commits:** Commit messages are short and imperative, e.g. `Fix docker`, `Add runs route`.
10. **PRs:** PR descriptions explain the change, list env/schema changes, and include screenshots for UI work.
11. **Attribution:** No `Co-Authored-By` trailers. AI usage is acknowledged in `README.md`.
12. **Guidance:** Document guidance changes in the same task's guidance files.
13. **Hooks/secrets:** `scripts/pre-commit` blocks `.env`, `.pem`, `.key`, etc.; install via `scripts/pre-commit`. `scripts/pre-push` automates Verification commands; install via `cp scripts/pre-push .git/hooks/pre-push && chmod +x .git/hooks/pre-push`. Hooks are opt-in; CI gitleaks in `.github/workflows/secrets-scan.yml` is authoritative and is chained into `deploy.yml` via `needs:`.

## Configuration & Secrets

Do not commit populated `.env` files or real credentials. See `example.env` for required variables and templates; keep local overrides out of version control. App settings use .NET options sections (`Section__Property`). Key vars: `Blizzard__ClientId`/`Blizzard__ClientSecret` (Blizzard OAuth, Key Vault refs in prod), `Blizzard__Region`, `Blizzard__RedirectUri`, `Blizzard__AppBaseUrl` (no trailing slash), `Cosmos__Endpoint`/`Cosmos__DatabaseName`, `Cors__AllowedOrigins__0` (frontend origin).

## Tool Configuration

**dotnet:** Use `dotnet` CLI directly. The solution targets .NET 10.

**GitHub:** Use `mcp__github__*` MCP tools for **all** GitHub interactions — PRs, issues, reviews, branch creation, file contents, repo search, and **reading files/code from external repositories**. **Do not use the `gh` CLI.** **Do not use WebSearch/WebFetch to browse GitHub** — use `mcp__github__get_file_contents`, `mcp__github__search_code`, `mcp__github__get_repository_tree`, and other MCP read tools instead. They return structured data, avoid rate limits, and work reliably. Subagents must follow the same rule.

**git:** **Always use `git -C <repo-root>` with an absolute path** — never `cd` into the repo then run bare `git`. This applies to every git command: status, add, commit, push, switch, diff, log, rebase. Subagents must follow the same rule. Bare `git` in a wrong cwd silently operates on the wrong repo.

**Config preference:** config file > CLI argument > environment variable.

**Sandbox:** Use `.cache/` (project-local, gitignored) as cache root — sandbox blocks `~/` writes. NuGet packages cache: `~/.nuget/packages` (CI caches this).

**Exit codes:** append `echo "EXIT:$?"` for consistent sandbox matching.

**JSON/YAML:** Use `jq` for JSON and `yq` for YAML. **No exceptions.** Never use Python (`python3 -c "import json/yaml"`), `sed`, `awk`, or any other tool to parse, validate, query, or transform structured data. This includes YAML validation — use `yq eval '.' file.yml`, not Python's `yaml.safe_load`. Subagents must follow the same rule.

## Verification

If `scripts/pre-push` is installed as a git hook (see Mandatory Git Workflow), the format / build / vulnerable-package commands below run automatically on `git push`. Otherwise run them manually before claiming work complete.

- Run `dotnet build lfm.sln -c Release` before claiming work complete for code, project, dependency, configuration, infrastructure, or generated-asset changes.
- Documentation-only changes that cannot affect build output may use targeted verification instead: review the changed files and run `git diff --check` against the changed documentation. State that the full build was intentionally skipped because the change is docs-only.
- Per-project tests: `dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release` / `dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release` / `dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release`.
- Format check: `dotnet format lfm.sln --verify-no-changes --no-restore --severity error`. **Run `dotnet format lfm.sln` before committing C# changes** — CI format check fails on whitespace.
- Bundle size check: `./scripts/check-bundle-size.sh ./publish/app/wwwroot 5` (after publish).
- Audit: `dotnet list lfm.sln package --vulnerable --include-transitive` after adding/upgrading packages.

## Testing

Three lanes with distinct purpose, environment, and file convention:

| Lane | Runner | Environment | File pattern | Location | Docker? |
|------|--------|-------------|--------------|----------|---------|
| Unit | xUnit | .NET (no DOM) | `*Tests.cs` | `tests/Lfm.Api.Tests/`, `tests/Lfm.App.Tests/` | No |
| Component | bUnit | DOM (bUnit) | `*Tests.cs` | `tests/Lfm.App.Tests/` | No |
| E2E | Playwright .NET | Chromium | `*Tests.cs` | `tests/Lfm.E2E/` | Yes |

### Choosing the right lane

| What you're testing | Lane |
|---------------------|------|
| Pure functions, utilities, data transforms, validation, API handlers with mocked deps | Unit |
| Blazor component rendering, state, events, interactions | Component (bUnit) |
| Auth flows, multi-step journeys, protected routes, axe audits, perf budgets | E2E |

### Unit tests

xUnit in .NET, no DOM. **Use for:** pure logic, validation, mocked API handlers. Commands: `dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj` / `dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj` / `dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj`. Config: each project's `.csproj`. Mutation testing: Stryker.NET runs against `tests/Lfm.App.Core.Tests/` only — the Blazor WASM project cannot be mutated because Stryker's recompile step does not invoke Razor source generators (see `docs/quality-reviews/2026-04-10-test-quality-audit.md`).

### Component tests

`tests/Lfm.App.Tests/` with bUnit + `AngleSharp`. **Use for:** Blazor component lifecycle, user interactions, rendered markup. **Not for:** pure logic (unit) or full-stack journeys (E2E). Commands: `dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj`.

### E2E tests

Playwright .NET against an in-process stack: Testcontainers brings up Cosmos + Azurite, the published API runs under `func start`, and the published Blazor `wwwroot` is served from an in-process Kestrel host. Requires a running Docker engine (for Testcontainers); no `docker compose` invocation.

Commands: `dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release`.

**Rules:** Only claim "full e2e passed" after running the full E2E suite. Keep coverage deterministic and local-first — no routine real Battle.net deps. **Bug fixes must include tests** — add or update tests that cover the fixed behavior. No bugfix is complete without a test that would have caught the regression.

## Infrastructure Development

All Bicep changes must follow **Azure Well-Architected Framework** (WAF). Before adding/modifying resources, use `microsoft-docs` skill to look up the WAF service guide. Apply all five pillars.

### WAF checklist for new resources

| Pillar | Check |
|--------|-------|
| **Security** | Managed identity over shared keys. TLS 1.2 min. Disable local auth where supported. RBAC over access policies. Disable FTP/basic auth. Key Vault references for secrets — never app settings. |
| **Reliability** | CanNotDelete lock on stateful resources (Cosmos, Storage, Key Vault). Soft delete / purge protection. Health check endpoints. |
| **Operational Excellence** | Diagnostic settings → Log Analytics. Fully parameterized modules — no hardcoded names/domains/regions. `@description` on every param. `@minLength`/`@maxLength` where Azure enforces. |
| **Cost Optimization** | Free-tier constraints (see above). Minimal Cosmos indexing — only queried paths; exclude `/*` on point-read containers. Log Analytics daily cap. |
| **Performance** | `http20Enabled` on web apps. Disable client affinity on stateless APIs. Per-document TTL over container-level when expiry varies. |

### WAF checklist for alerts

Verify `timeAggregation` is valid for the chosen metric via `microsoft-docs`. Dimension-filtered metrics (e.g. Cosmos `TotalRequests` with `StatusCode`) often require `Count`, not `Total`. Always set `autoMitigate: true`.

### Workflow parameterization

Deploy workflows use GitHub repo variables for all project-specific values. Never hardcode in workflows.

| Variable | Purpose |
|----------|---------|
| `AZURE_RESOURCE_GROUP` | Target resource group |
| `AZURE_LOCATION` | Azure region |
| `COSMOS_ACCOUNT_NAME` | Cosmos DB account name |
| `STORAGE_ACCOUNT_NAME` | Storage account name |
| `FUNCTION_APP_NAME` | Functions app name |
| `SWA_NAME` | Static Web App name |
| `KEY_VAULT_NAME` | Key Vault name |
| `LOG_ANALYTICS_NAME` | Log Analytics workspace |
| `COSMOS_DATABASE` | Cosmos DB database name |
| `API_HOSTNAME` | API custom domain |
| `FRONTEND_HOSTNAME` | Frontend custom domain |
| `PRIVACY_EMAIL` | Privacy contact email |
| `BATTLE_NET_REGION` | Battle.net region code |

`frontendOrigin`, `battleNetRedirectUri`, and `tags` are derived from the above in the workflow.

### `az` CLI usage

Acceptable for cleanup or exploration or debugging. Any `az` CLI change is either **cleanup** or **temporary and must be reverted**. The fix must be captured in Bicep (`infra/`) or `deploy-infra.yml`. Production state is always defined by `deploy-infra.yml`.

### Validation

`analyze-infra.yml` runs PSRule on every push/PR touching `infra/` (config: `ps-rule.yaml`). Verify the `Analyze Infrastructure` check passes before merging. Suppress rules in `ps-rule.yaml` `rule.exclude` with a justifying comment.

### Structure

`infra/main.bicep` orchestrates `infra/modules/`. New params → add to both `main.bicep` and `infra/parameters.example.json`, and add a corresponding GitHub variable to `deploy-infra.yml` inline overrides. Module params are required with no defaults. `parameters.example.json` serves as a reference for local deployments.

## Azure Functions

New endpoint → add a new file under `api/Functions/` and register `app.MapXxx()` (or use attribute routing on the function class). The .NET isolated model auto-discovers functions in the assembly; no manual import list needed.

## Storage Architecture

Static Blizzard reference data (journal-instance, playable-specialization, playable-class, playable-race, hero-talent-tree) lives in blob at `lfmstore/wow/reference/{kind}/`. Dynamic per-user / per-guild / per-run data lives in Cosmos (`lfm-cosmos/lfm/{raiders,runs,guilds}`). Per-entity caches of Blizzard responses (e.g. one user's account profile, one guild's roster) stay **embedded** inside the owning Cosmos document with a `*FetchedAt` timestamp, not in blob.

See [docs/storage-architecture.md](docs/storage-architecture.md) for the full data-kind matrix, rationale, image-caching policy (URL caches only — browser HTTP cache handles bytes), and the decision flow for a new data kind. When adding something new that needs to persist, start there.

**Never hardcode the Cosmos database name.** Read `Cosmos__DatabaseName` from configuration — E2E runs against a different database than production.

There is no DB migration runner. Reference data refresh is handled by `WowReferenceRefreshFunction` (admin-only `POST /api/wow/reference/refresh`) and `WowReferenceRefreshTimerFunction` (weekly), both writing to blob.

New reference-data accessors should reuse `IBlobReferenceClient` plus a focused projection helper rather than create a new `I*Repository` interface unless a second implementation or test seam need is real.

## API Wire Contracts

Every property on a `shared/Lfm.Contracts/` record must have a live consumer in `app/` markup or `app/Lfm.App.Core/`. Test-only usage does not qualify. Server-internal state (Cosmos `Ttl`, audit timestamps, other users' Battle.net ids, raw Blizzard pass-through payloads) never goes on the wire — project to a Lfm-owned DTO at the `api/Functions/` boundary. Two narrow exceptions (peer permission fields, planned near-term feature reservations) require an XML doc-comment naming the reason. See [docs/wire-payload-contract.md](docs/wire-payload-contract.md).

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, architectural decisions. User-facing content belongs in `README.md` or `docs/user/`. Plans and specs: `docs/superpowers/plans/` and `docs/superpowers/specs/`.
