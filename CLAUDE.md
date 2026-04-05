# Repository Guidelines

- `frontend/` — Vite React SPA → Static Web Apps
- `functions/` — Azure Functions backend (Node.js)
- `infra/` — Bicep IaC templates

When working on Azure-related tasks, use the `microsoft-docs` skill to look up official documentation before making changes.

## Cost Guidance

Hobby project. Prefer free tiers: Cosmos DB free tier, Functions Consumption (Y1), Static Web Apps Free, workspace-based App Insights sharing Log Analytics 5 GB/month free ingestion. Small fixed costs (Key Vault ops, Storage LRS) are fine. Avoid significant recurring costs without discussing first.

## Mandatory Git Workflow

1. Start every task with a clean workspace; stop and alert if not clean.
2. Work in `claude/<short-slug>` via `git switch -c`. Always use `git -C` with absolute paths.
3. Keep changesets small: commits ≤ 5 files / ≤ 250 lines; branches ≤ 30 files / ≤ 900 lines vs `main`. Thresholds guide planning, not design. Commit partial finishes; split into subtasks if exceeding.
4. Merge strategy: rebase-and-merge. Use `superpowers:finishing-a-development-branch` to close a branch.
5. Before claiming complete: `superpowers:verification-before-completion`. Non-trivial tasks: `superpowers:requesting-code-review` before merging.
6. Commit messages: short, imperative — e.g. `Fix docker`, `Add runs route`.
7. PR descriptions: explain the change, list env/schema changes, include screenshots for UI work.
8. No `Co-Authored-By` trailers. AI usage acknowledged in `README.md`.
9. Document guidance changes in the same task's guidance files.
10. **Pre-commit hook** (`scripts/pre-commit`) blocks `.env`, `.pem`, `.key`, etc. Install via `scripts/pre-commit`.

## Configuration & Secrets

Do not commit populated `.env` files or real credentials. See `example.env` for required variables and templates; keep local overrides out of version control. Key vars: `LFM_CLIENT_ID`/`LFM_CLIENT_SECRET` (Blizzard OAuth), `BATTLE_NET_REGION`, `BATTLE_NET_REDIRECT_URI`/`APP_BASE_URL` (no trailing slash), `HMAC_SECRET` (64 hex chars via `openssl rand -hex 32`).

## Tool Configuration

**fnm:** Always prefix `node`/`pnpm` calls with `fnm exec`. Never call them directly. Project uses `.node-version` + corepack for pnpm.

**Config preference:** config file > CLI argument > environment variable.

**Sandbox:** Use `.cache/` (project-local, gitignored) as cache root — sandbox blocks `~/` writes. pnpm store: `frontend/.npmrc`; ESLint cache: `--cache-location` in lint scripts. Prefer `-C` flags over `cd &&`:

```bash
# Good
fnm exec pnpm -C /absolute/path/frontend install
# Avoid
cd frontend && pnpm install
```

**Exit codes:** append `echo "EXIT:$?"` for consistent sandbox matching. **JSON:** use `jq`, not Python one-liners. **YAML:** use `yq`, not Python one-liners.

## Verification

- Run `./scripts/verify-local.sh fast` before claiming work complete (lint + tests + build + bundle check, both packages).
- Per-package: `fnm exec pnpm -C frontend verify:fast` / `fnm exec pnpm -C functions verify:fast`.
- `./scripts/verify-local.sh browser` adds E2E; `full` adds perf specs.
- Run `fnm exec pnpm -C <package> audit` after adding/upgrading deps — deploy workflows fail on vulnerabilities.
- **Knip** for unused exports/files: `fnm exec pnpm -C frontend exec knip` / `fnm exec pnpm -C functions exec knip`.

## Testing

Three lanes with distinct purpose, environment, and file convention:

| Lane | Runner | Environment | File pattern | Location | Docker? |
|------|--------|-------------|--------------|----------|---------|
| Unit | Vitest | Node (no DOM) | `*.test.ts` | `frontend/src/`, `functions/src/` | No |
| Integration | Vitest | jsdom | `*.integration.test.tsx` | `frontend/src/` | No |
| E2E | Playwright | Chromium | `*.spec.ts` | `frontend/e2e/` | Yes |

### Choosing the right lane

| What you're testing | Lane |
|---------------------|------|
| Pure functions, utilities, data transforms, validation, backend handlers with mocked deps | Unit |
| Component structure via `renderToStaticMarkup` (no interaction needed) | Unit |
| Interactive behavior (clicks, menus, state), responsive breakpoints, accessible markup | Integration |
| Auth flows, multi-step journeys, protected routes, axe audits, perf budgets | E2E |

### Unit tests

Vitest in Node (`environment: "node"`), no DOM. **Use for:** pure logic, validation, mocked handlers, static component structure (`renderToStaticMarkup`). **Not for:** click handlers, state changes, DOM events. Commands: `fnm exec pnpm -C frontend test:unit` / `fnm exec pnpm -C functions test`. Config: `frontend/vitest.config.ts`, `functions/vitest.config.ts`.

### Integration tests

Frontend-only. Vitest + jsdom + `@testing-library/react` + `userEvent`. **Use for:** DOM interaction — clicks, keyboard nav, responsive layout, `aria-expanded`, focus management. Render with `renderWithProviders` (MemoryRouter + ThemeRegistry + AuthContext). **Not for:** pure logic (unit) or full-stack journeys (E2E). Commands: `fnm exec pnpm -C frontend test:integration`. Config: `frontend/vitest.integration.config.ts`. Helpers: `frontend/src/test/renderWithProviders.tsx`, `setupDomTests.ts`.

### E2E tests

Playwright against full Docker stack (Cosmos, Azurite, Functions, frontend preview). `scripts/dev-env.mjs` manages lifecycle. Scenarios: `default`, `runs-empty`, `runs-error`, `characters-empty`, `instances-missing` — each seeds differently. Perf specs in `frontend/e2e/perf/`, excluded from default discovery.

Commands: `fnm exec pnpm -C frontend e2e:list`, `./scripts/dev-env.mjs test signup`, `./scripts/e2e.sh runs-empty runs-empty.spec.ts`, `./scripts/e2e-all.sh` (full suite).

**Rules:** Only claim "full e2e passed" after `./scripts/e2e-all.sh`. Default runs are single-worker (shared seed state). New scenario spec → update `E2E_SCENARIOS` in `scripts/dev-env.mjs`, `SCENARIO_SPECS` in `frontend/playwright.config.ts`, and `scripts/e2e-all.sh`. New perf spec → update `PERF_SPECS` in `frontend/playwright.config.ts` and `run_perf` in `scripts/verify-local.sh`. Keep coverage deterministic and local-first — no routine real Battle.net deps.

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

| Variable | Purpose | Example |
|----------|---------|---------|
| `AZURE_RESOURCE_GROUP` | Target resource group | `lfm` |
| `AZURE_LOCATION` | Azure region | `westeurope` |
| `FUNCTION_APP_NAME` | Functions app name | `lfm-functions` |
| `KEY_VAULT_NAME` | Key Vault name | `lfm-kv-prot` |
| `SWA_NAME` | Static Web App name | `lfm-swa` |
| `LOG_ANALYTICS_NAME` | Log Analytics workspace | `lfm-logs` |
| `API_HOSTNAME` | API custom domain | `lfm-api.dinosauruskeksi.com` |
| `FRONTEND_HOSTNAME` | Frontend custom domain | `lfm.dinosauruskeksi.com` |
| `PARAMETER_FILE` | Bicep parameter file path | `infra/parameters.prod.lfm.json` |
| `PRIVACY_EMAIL` | Privacy contact email | *(set in GitHub)* |

`VITE_API_BASE_URL` is derived from `API_HOSTNAME` in the frontend workflow — not a separate var.

### `az` CLI usage

Acceptable for exploration/debugging. Any `az` CLI change is **temporary and must be reverted**. The fix must be captured in Bicep (`infra/`) or `deploy-infra.yml`. Production state is always defined by `deploy-infra.yml`.

### Validation

`analyze-infra.yml` runs PSRule on every push/PR touching `infra/` (config: `ps-rule.yaml`). Verify the `Analyze Infrastructure` check passes before merging. Suppress rules in `ps-rule.yaml` `rule.exclude` with a justifying comment.

### Structure

`infra/main.bicep` orchestrates `infra/modules/`. New params → add to both `main.bicep` and `infra/parameters.example.json`. Module params are required with no defaults — expanded through `main.bicep` via parameter files. Production param files (`parameters.prod.*.json`) are gitignored.

## Azure Functions

New endpoint file → add `import "./functions/<name>.js"` to `functions/src/index.ts`. The v4 runtime only registers `app.http()` calls from files imported by the entrypoint; missing imports produce silent 404s (tests pass, deploy succeeds).

## Database Migrations

Migrations in `functions/src/scripts/migrations/` run via umzug before every functions deploy. The migrations container in Cosmos tracks execution; each runs at most once.

**Migrations must be additive (expand-only)** — never remove/rename fields the deployed code reads. Migrations run before new code deploys; if deploy fails, old code runs against the migrated database.

For breaking changes, use expand/contract: (1) **Expand** — add new field alongside old; deploy code handling both. (2) **Contract** — later deploy removes old field once no code references it.

**Never hardcode the database name.** Use `process.env.COSMOS_DATABASE!` — migrations run against different databases per environment. Manual run: `COSMOS_ENDPOINT=<endpoint> COSMOS_DATABASE=lfm fnm exec pnpm -C functions exec tsx src/scripts/run-migrations.ts`.

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, architectural decisions. User-facing content belongs in `README.md` or `docs/user/`. Plans and specs: `docs/superpowers/plans/` and `docs/superpowers/specs/`.
