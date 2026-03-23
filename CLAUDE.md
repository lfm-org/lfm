# Repository Guidelines

Project structure has migrated to Azure:
- `frontend/` — Vite-based React SPA (deploying to Static Web Apps)
- `functions/` — Azure Functions backend (Node.js runtime)
- `infra/` — Infrastructure as Code (Bicep templates)

For migration details, see `docs/superpowers/plans/`.

## Cost Guidance

This is a hobby project. Prefer free tiers where available — Cosmos DB free tier, Functions Consumption (Y1), Static Web Apps Free, and workspace-based App Insights sharing the Log Analytics 5 GB/month free ingestion. Small fixed costs (Key Vault operations, Storage LRS) are fine. When adding or changing infrastructure, avoid introducing resources or SKUs that would create significant recurring costs without discussing it first.

## Mandatory Git Workflow

1. Start every task with a clean workspace; stop and alert the user if not clean.
2. Work in a dedicated branch: `claude/<short-slug>` — use `git switch -c claude/<short-slug>`. Prefer `git switch -c` over `git checkout -b`. Always use `git -C` with absolute paths.
3. Keep changesets small: commits `<= 5` files / `<= 250` lines; branches `<= 30` files / `<= 900` lines vs `main`. Thresholds guide planning, not design — don't degrade quality to hit them. Commit partial finishes as you go; split into subtasks if a branch will exceed these.
4. Merge strategy is rebase-and-merge. Use `superpowers:finishing-a-development-branch` to close a branch; keep workspace clean on `main` after close.
5. Before claiming work complete, use `superpowers:verification-before-completion`. For non-trivial tasks, use `superpowers:requesting-code-review` before merging.
6. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add raids route`.
7. PR descriptions: explain the change, list env/schema changes, include screenshots for UI work.
8. Do not add `Co-Authored-By` trailers. AI usage is acknowledged in `README.md`.
9. Document any guidance changes in the same task's guidance files.

## Configuration & Secrets

Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates; keep local overrides out of version control.

**Required environment variables** (see `example.env` for full list):
- `LFM_CLIENT_ID` / `LFM_CLIENT_SECRET` — Blizzard OAuth app credentials
- `BATTLE_NET_REGION` — e.g. `eu`
- `BATTLE_NET_REDIRECT_URI` / `APP_BASE_URL` — public URLs (no trailing slash; never hardcode localhost in production paths)
- `BATTLE_NET_COOKIE_SECURE` — `true` in production, `false` in local dev
- `HMAC_SECRET` — 64 random hex chars; generate with `openssl rand -hex 32`

## Tool Configuration

When configuring any tool (linters, bundlers, test runners, package managers, etc.) prefer configuration methods in this order:

1. **Config file** — use the tool's dedicated config file (`eslint.config.js`, `tsconfig.json`, `jest.config.ts`, `.npmrc`, etc.) where supported. Config files are version-controlled, discoverable, and IDE-aware.
2. **CLI argument** — use flags when the option isn't supported in a config file, or for one-off overrides (e.g. `--cache-dir`).
3. **Environment variable** — use env vars only when config files and CLI arguments are unavailable for the option.

### Sandbox cache root

Claude Code runs in a sandboxed environment where writes to `~/` locations (e.g. `~/.npm`, `~/.cache`, `~/.config`) are blocked. Use `.cache/` (project-local, git-ignored) as the cache root for any tool that cannot use its default location:

| Tool | Config file approach (preferred) |
|------|----------------------------------|
| npm | `cache=.cache/npm` in `frontend/.npmrc` |
| ESLint | `cacheLocation: ".cache/eslint"` in `eslint.config.js` |

`.cache/` is git-ignored (contents only; `.cache/.gitkeep` is tracked so the directory exists in fresh checkouts).

### Running tools in subdirectories

Prefer `--prefix` / `-C` flags over `cd dir &&` to run tools in subdirectories. The sandbox allow-list is keyed on command prefixes like `git -C /absolute/path:*` and `npm --prefix /absolute/path/subdir:*` — using `cd` first produces a different command string that won't match, triggering an unnecessary permission prompt even for an already-allowed operation.

```bash
# Good — matches sandbox allow-list
git -C /absolute/path/to/repo status
npm --prefix /absolute/path/to/repo/frontend install
npx --prefix /absolute/path/to/repo/frontend tsc --noEmit

# Avoid — cd changes the command string, bypasses allow-list match
cd frontend && npm install
cd frontend && npx tsc --noEmit
```

### Script exit codes

When checking a command's exit code in Bash, use the format `echo "EXIT:$?"` (uppercase, colon, no spaces). One consistent format keeps the sandbox allow-list clean:

```bash
npm --prefix frontend run build 2>&1; echo "EXIT:$?"
```

### JSON processing

Use `jq` for any JSON processing in shell, not Python. Python one-liners for JSON are verbose, often require multiple iterations to produce the expected output, and are harder to read at a glance. `jq` is purpose-built, composable, and produces correct results first try.

```bash
# Good
jq '.raids[] | select(.visibility == "PUBLIC") | .id' raids.json

# Avoid
python3 -c "import json,sys; data=json.load(open('raids.json')); print([r['id'] for r in data['raids'] if r['visibility']=='PUBLIC'])"
```

## E2E Testing

Playwright coverage lives in `frontend/e2e/`. Add or update e2e tests when a change affects auth/login/logout behavior, protected routes, seeded `TEST_MODE` flows, multi-step create/update/cancel journeys, public entry pages, accessibility-critical interactions, or a regression that escaped unit/type-level coverage.

Useful commands:
- list the default-discovered specs: `npm --prefix frontend run e2e:list`
- run a focused default-scenario spec: `./scripts/dev-env.mjs test signup`
- run a scenario-specific spec: `./scripts/e2e.sh raids-empty raids-empty.spec.ts`
- run the intended full all-scenarios suite: `./scripts/e2e-all.sh`

Rules for agents:
- Do not claim "full e2e suite passed" unless you ran `./scripts/e2e-all.sh`.
- Default Playwright discovery excludes the scenario-specific files listed in `frontend/playwright.config.ts`.
- If you add a new scenario-only spec, update both `frontend/playwright.config.ts` and `scripts/e2e-all.sh`.
- Keep e2e coverage deterministic and local-first. Do not add routine real Battle.net dependencies to the normal Playwright workflow.

## Infrastructure Development

`az` CLI commands are acceptable for **exploration and debugging** during infra development (e.g. inspecting resource state, testing a hypothesis). However:

1. Any `az` CLI change to Azure resources is **temporary and must be reverted** once the investigation is done.
2. The fix must be captured in Bicep templates (`infra/`) or the `deploy-infra.yml` workflow.
3. Production state is always defined by `deploy-infra.yml` — never by ad-hoc CLI commands. All infrastructure changes ship through that workflow.

## Database Migrations

Migrations live in `functions/src/scripts/migrations/` and run via umzug before every functions deploy (see `.github/workflows/deploy-functions.yml`). The migrations container in Cosmos tracks which have run; each migration executes at most once.

**Migrations must be additive (expand-only).** They may add fields, backfill data, or create new documents — never remove or rename fields that the currently-deployed code reads. This is because migrations run before the new function code is deployed: if the deploy fails, the old code keeps running against the already-migrated database.

Follow the expand/contract pattern for breaking changes:
1. **Expand** — add the new field/shape alongside the old one; deploy code that handles both.
2. **Contract** — in a later deploy, remove the old field once no code references it.

**Never hardcode the database name** in migration files. Always use `process.env.COSMOS_DATABASE!` — migrations run against different databases in different environments (`lfm` in production, `lfm-e2e` in test).

To run migrations manually:
```bash
COSMOS_ENDPOINT=<endpoint> COSMOS_DATABASE=lfm npx tsx functions/src/scripts/run-migrations.ts
```

## LSP Tool

The LSP tool provides TypeScript language server intelligence. Use it for **code navigation and type inspection** — it does not surface diagnostics (use `tsc --noEmit` for that).

Prefer LSP over reading files when:
- **Resolving a type** — `hover` on an expression to see its inferred type without reading surrounding declarations
- **Tracing a call** — `incomingCalls`/`outgoingCalls` to map where a function is called or what it calls
- **Finding usages before renaming or deleting** — `findReferences` to ensure nothing is missed
- **Jumping to a definition across packages** — `goToDefinition` when a symbol comes from a dependency

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions. Do not mix in user-facing content — that belongs in `README.md` or `docs/user/`.

Plans and specs live in `docs/superpowers/plans/` and `docs/superpowers/specs/` respectively.
