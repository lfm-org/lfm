# Repository Guidelines

Project structure has migrated to Azure:
- `frontend/` — Vite-based React SPA (deploying to Static Web Apps)
- `functions/` — Azure Functions backend (Node.js runtime)
- `infra/` — Infrastructure as Code (Bicep templates)

For migration details, see `docs/superpowers/plans/`.

For frontend styling conventions, see `docs/frontend-style-guide.md`.

When working on Azure-related tasks (infrastructure, deployment, Functions, Static Web Apps, Cosmos DB, Bicep, etc.), use the `microsoft-docs` skill to look up official documentation before making changes.

## Cost Guidance

This is a hobby project. Prefer free tiers where available — Cosmos DB free tier, Functions Consumption (Y1), Static Web Apps Free, and workspace-based App Insights sharing the Log Analytics 5 GB/month free ingestion. Small fixed costs (Key Vault operations, Storage LRS) are fine. When adding or changing infrastructure, avoid introducing resources or SKUs that would create significant recurring costs without discussing it first.

## Mandatory Git Workflow

1. Start every task with a clean workspace; stop and alert the user if not clean.
2. Work in a dedicated branch: `claude/<short-slug>` — use `git switch -c claude/<short-slug>`. Prefer `git switch -c` over `git checkout -b`. Always use `git -C` with absolute paths.
3. Keep changesets small: commits `<= 5` files / `<= 250` lines; branches `<= 30` files / `<= 900` lines vs `main`. Thresholds guide planning, not design — don't degrade quality to hit them. Commit partial finishes as you go; split into subtasks if a branch will exceed these.
4. Merge strategy is rebase-and-merge. Use `superpowers:finishing-a-development-branch` to close a branch; keep workspace clean on `main` after close.
5. Before claiming work complete, use `superpowers:verification-before-completion`. For non-trivial tasks, use `superpowers:requesting-code-review` before merging.
6. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add runs route`.
7. PR descriptions: explain the change, list env/schema changes, include screenshots for UI work.
8. Do not add `Co-Authored-By` trailers. AI usage is acknowledged in `README.md`.
9. Document any guidance changes in the same task's guidance files.
10. **Pre-commit hook:** `scripts/pre-commit` blocks `.env`, `.pem`, `.key`, and similar sensitive files. Install with `cp scripts/pre-commit .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit`.

## Configuration & Secrets

Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates; keep local overrides out of version control.

**Required environment variables** (see `example.env` for full list):
- `LFM_CLIENT_ID` / `LFM_CLIENT_SECRET` — Blizzard OAuth app credentials
- `BATTLE_NET_REGION` — e.g. `eu`
- `BATTLE_NET_REDIRECT_URI` / `APP_BASE_URL` — public URLs (no trailing slash; never hardcode localhost in production paths)
- `BATTLE_NET_COOKIE_SECURE` — `true` in production, `false` in local dev
- `HMAC_SECRET` — 64 random hex chars; generate with `openssl rand -hex 32`

## Tool Configuration

### Node.js via fnm

This project uses **fnm** (Fast Node Manager) with `.node-version` to pin the Node version and **pnpm** as the package manager (provided via corepack). Never call `node` or `pnpm` directly — always prefix with `fnm exec`. This keeps sandbox allow-list entries simple (e.g. `fnm exec:*`).

### Config file preference

When configuring any tool, prefer: **config file** (version-controlled, IDE-aware) > **CLI argument** (for unsupported or one-off options) > **environment variable** (only if no other option).

### Sandbox & commands

Claude Code's sandbox blocks writes to `~/` locations. Use `.cache/` (project-local, git-ignored) as the cache root:

| Tool | Config file approach |
|------|---------------------|
| pnpm | `store-dir=.cache/pnpm-store` in `frontend/.npmrc` |
| ESLint | `cacheLocation: ".cache/eslint"` in `eslint.config.mjs` |

Prefer `--prefix` / `-C` flags over `cd dir &&` — the sandbox allow-list is keyed on command prefixes:

```bash
# Good — matches sandbox allow-list
git -C /absolute/path/to/repo status
fnm exec pnpm -C /absolute/path/to/repo/frontend install
fnm exec pnpm -C /absolute/path/to/repo/frontend exec tsc --noEmit

# Avoid — direct calls bypass fnm; cd bypasses allow-list match
pnpm -C /absolute/path/to/repo/frontend install
cd frontend && pnpm install
```

Exit codes: use `echo "EXIT:$?"` (uppercase, colon, no spaces) for consistent sandbox allow-list matching:

```bash
fnm exec pnpm -C frontend run build 2>&1; echo "EXIT:$?"
```

### JSON processing

Use `jq` for JSON processing in shell, not Python one-liners.

```bash
jq '.runs[] | select(.visibility == "PUBLIC") | .id' runs.json
```

## Verification

Run `./scripts/verify-local.sh` before claiming work is complete:

| Level | Command | What it runs |
|-------|---------|-------------|
| Fast | `./scripts/verify-local.sh fast` | lint + unit tests + integration tests + build + bundle check (both packages) |
| Browser | `./scripts/verify-local.sh browser` | fast + all E2E scenarios |
| Full | `./scripts/verify-local.sh full` | browser + perf specs |

Per-package shortcuts:
- `fnm exec pnpm -C frontend verify:fast` — lint, unit + integration tests, build, bundle check
- `fnm exec pnpm -C functions verify:fast` — lint, build, unit tests

Run `fnm exec pnpm -C <package> audit` after adding or upgrading dependencies — deploy workflows run `pnpm audit` and will fail on vulnerabilities.

**Knip** is installed in both packages for unused export/file detection: `fnm exec pnpm -C frontend exec knip` / `fnm exec pnpm -C functions exec knip`.

## Testing

Three test lanes, each with a distinct purpose, environment, and file convention. Choosing the wrong lane wastes time and produces brittle coverage.

| Lane | Runner | Environment | File pattern | Location | Docker? |
|------|--------|-------------|--------------|----------|---------|
| Unit | Vitest | Node (no DOM) | `*.test.ts` | `frontend/src/`, `functions/src/` | No |
| Integration | Vitest | jsdom | `*.integration.test.tsx` | `frontend/src/` | No |
| E2E | Playwright | Chromium | `*.spec.ts` | `frontend/e2e/` | Yes |

### Choosing the right lane

| What you're testing | Lane | Why |
|---------------------|------|-----|
| Pure function, utility, data transform, validation | Unit | No DOM needed; fastest feedback |
| Component structure via `renderToStaticMarkup` | Unit | Server render works in Node; no interaction needed |
| Backend handler logic with mocked deps | Unit | Isolate from real services |
| Component interactive behavior (clicks, menus, state) | Integration | Needs DOM + React event system; too slow/fragile for E2E |
| Responsive breakpoints, media queries | Integration | `matchMedia` shim in jsdom; Playwright for visual confirmation |
| Accessible markup (`aria-*`, roles, focus management) | Integration | Fast structural checks; pair with E2E axe audit |
| Auth/login/logout flows | E2E | Requires real cookie + redirect handling |
| Multi-step user journeys (create → edit → delete) | E2E | Needs full stack + seeded database |
| Protected routes, permission boundaries | E2E | Middleware + backend auth required |
| Accessibility compliance (axe audit) | E2E | Real browser rendering required |
| Performance budgets | E2E (perf) | Real browser timing required |

### Unit tests

Both packages use **Vitest** in Node environment (`environment: "node"`). No DOM, no jsdom.

**What belongs here:** Pure logic, utilities, data transformations, validation rules, mocked backend handlers. Frontend component tests that only need structure (use `renderToStaticMarkup`, not `@testing-library/react`).

**What does NOT belong:** Anything requiring click handlers, state changes, DOM events, `useMediaQuery`, or interactive behavior — use integration tests instead.

Commands:
- `fnm exec pnpm -C frontend test:unit` — frontend unit tests
- `fnm exec pnpm -C functions test` — functions unit tests
- Watch mode: `test:unit:watch` (frontend) / `test:watch` (functions)

Config: `frontend/vitest.config.ts` (includes `src/**/*.test.ts`), `functions/vitest.config.ts`.

### Integration tests

Frontend-only. Vitest with **jsdom** environment, `@testing-library/react`, and `userEvent`.

**What belongs here:** Component behavior that requires a DOM — click handlers, menu open/close, keyboard navigation, responsive layout changes via `matchMedia`, `aria-expanded` toggling, focus management. These tests render components with `renderWithProviders` (wraps MemoryRouter + ThemeRegistry + AuthContext) and assert on interactive behavior.

**What does NOT belong:** Pure logic (use unit tests). Full user journeys that need a real backend, cookies, or database (use E2E).

Commands:
- `fnm exec pnpm -C frontend test:integration` — run integration tests
- `fnm exec pnpm -C frontend test` — run both unit + integration

Config: `frontend/vitest.integration.config.ts` (includes `src/**/*.integration.test.tsx`). Setup file: `src/test/setupDomTests.ts` (cleanup + `matchMedia` shim).

Shared helpers in `frontend/src/test/`:
- `renderWithProviders.tsx` — render with MemoryRouter, ThemeRegistry, AuthContext
- `setupDomTests.ts` — `afterEach(cleanup)`, `matchMedia` shim, `setViewportWidth()`

### E2E tests

Playwright against the full Docker stack (Cosmos, Azurite, Functions, frontend preview server).

**What belongs here:** Auth flows, protected routes, multi-step journeys, form submissions with real backend, accessibility audits (axe), scenario-dependent state.

`scripts/dev-env.mjs` manages the Docker stack lifecycle (start, seed, run Playwright, teardown).

Available scenarios: `default`, `runs-empty`, `runs-error`, `characters-empty`, `instances-missing`. Each seeds the database differently; scenario-specific specs only pass under their matching scenario.

Perf specs live in `frontend/e2e/perf/` and are excluded from default discovery.

Commands:
- `fnm exec pnpm -C frontend e2e:list` — list default-discovered specs
- `./scripts/dev-env.mjs test signup` — run a focused spec (bare names expanded to `e2e/*.spec.ts`)
- `./scripts/e2e.sh runs-empty runs-empty.spec.ts` — run a scenario-specific spec
- `./scripts/e2e-all.sh` — full all-scenarios suite

Rules:
- Do not claim "full e2e suite passed" unless you ran `./scripts/e2e-all.sh`.
- Default Playwright runs are single-worker (shared Docker seed state). Do not raise parallelism unless specs are state-isolated.
- New scenario spec → update `E2E_SCENARIOS` in `scripts/dev-env.mjs`, `SCENARIO_SPECS` in `frontend/playwright.config.ts`, and `scripts/e2e-all.sh`.
- New perf spec → update `PERF_SPECS` in `frontend/playwright.config.ts` and `run_perf` in `scripts/verify-local.sh`.
- Keep coverage deterministic and local-first. No routine real Battle.net dependencies in the Playwright workflow.

## Infrastructure Development

All Bicep changes must follow Azure Well-Architected Framework best practices. Use the `microsoft-docs` skill to verify resource configurations against current WAF guidance before committing.

`az` CLI commands are acceptable for **exploration and debugging** (e.g. inspecting resource state). However:

1. Any `az` CLI change to Azure resources is **temporary and must be reverted** once the investigation is done.
2. The fix must be captured in Bicep templates (`infra/`) or the `deploy-infra.yml` workflow.
3. Production state is always defined by `deploy-infra.yml` — never by ad-hoc CLI commands.

**Validation:** The `analyze-infra.yml` workflow runs PSRule for Azure on every push/PR touching `infra/`. Configuration is in `ps-rule.yaml`. After modifying infra, push to a branch and verify the `Analyze Infrastructure` check passes before merging. If a rule must be suppressed (e.g. Consumption plan limitations), add it to `ps-rule.yaml` `rule.exclude` with a justifying comment.

**Structure:** `infra/main.bicep` orchestrates modules in `infra/modules/`. New params must be added to both `main.bicep` and `infra/parameters.prod.lfm.json`. Module params are required with no defaults — they are expanded through `main.bicep` via parameter-file expansion, not standalone.

## Database Migrations

Migrations live in `functions/src/scripts/migrations/` and run via umzug before every functions deploy (see `.github/workflows/deploy-functions.yml`). The migrations container in Cosmos tracks which have run; each migration executes at most once.

**Migrations must be additive (expand-only).** They may add fields, backfill data, or create new documents — never remove or rename fields that the currently-deployed code reads. This is because migrations run before the new function code is deployed: if the deploy fails, the old code keeps running against the already-migrated database.

Follow the expand/contract pattern for breaking changes:
1. **Expand** — add the new field/shape alongside the old one; deploy code that handles both.
2. **Contract** — in a later deploy, remove the old field once no code references it.

**Never hardcode the database name** in migration files. Always use `process.env.COSMOS_DATABASE!` — migrations run against different databases in different environments (`lfm` in production, `lfm-e2e` in test).

To run migrations manually:
```bash
COSMOS_ENDPOINT=<endpoint> COSMOS_DATABASE=lfm fnm exec pnpm -C functions exec tsx src/scripts/run-migrations.ts
```

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions. Do not mix in user-facing content — that belongs in `README.md` or `docs/user/`.

Plans and specs live in `docs/superpowers/plans/` and `docs/superpowers/specs/` respectively.
