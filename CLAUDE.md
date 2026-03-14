# Repository Guidelines

## Project Structure & Module Organization
This is a full-stack Next.js 15 + Prisma app. All code lives under `frontend/`.

```
frontend/
  app/
    api/          ← Next.js Route Handlers (battlenet, raids, health, wow/update)
    layout.tsx    ← ThemeRegistry, NavBar, global CSS
    page.tsx      ← redirects to /raids
    login/        ← login, success, failed pages
    raids/        ← raids list and [id] detail pages
  lib/
    prisma.ts     ← Prisma client singleton (lazy Proxy — see Gotchas)
    battlenet.ts  ← OAuth2 logic (no DI, plain module)
    auth.ts       ← requireAuth helper
  components/     ← ThemeRegistry, NavBar, Logo
  util/           ← ApiUtil, DateUtil
  prisma/
    schema.prisma ← single source of truth for all models
    migrations/
  prisma.config.ts ← Prisma CLI config (datasource url lives here, not schema.prisma)
  middleware.ts   ← protects /raids/* routes
```

Root `docker-compose.yml` has 2 services: `frontend` (Next.js) + `database` (PostgreSQL).

## Build, Test, and Development Commands
Set up env files: `cp example.env .env` and `cp frontend/example.env frontend/.env.local`.

- `docker compose up --build`: runs Next.js and PostgreSQL. `docker-compose.override.yml` enables hot-reload dev (port 3001).
- `cd frontend && npm install && npm run dev`: starts Next.js dev server on port 3001.
- `cd frontend && npm run build`: builds with `prisma generate && next build`.
- `cd frontend && npm test`: runs Jest / Testing Library.
- `cd frontend && npm test -- --watch`: run tests in watch mode.
- `npx playwright test`: runs E2E tests (requires app running; see `playwright/`).

## Coding Style & Naming Conventions
Use TypeScript throughout with strict mode. Keep double quotes and semicolons, follow the surrounding file's indentation. Use `PascalCase` for React components; `camelCase` for variables, functions, and Prisma field names. Next.js Route Handlers live in `app/api/**/route.ts`.

## Behavior-Driven Development (BDD)

Use the `superpowers:test-driven-development` skill for the TDD workflow. Project-specific conventions:

**Naming pattern** — use `Given / When / Then` in `describe` and `it` strings:

```typescript
describe("given a raid exists", () => {
  describe("when GET /api/raids is called without a cookie", () => {
    it("then it returns 401", async () => { ... });
  });
});
```

**Principles:**
- Test from the outside in: HTTP route → service behavior → DB state.
- For React components, use Testing Library with user-centric queries (`getByRole`, `getByText`) — never query by class name or internal state.
- One assertion per scenario where possible; name the scenario in the `it` string.
- Do **not** test implementation details (which Prisma method was called, internal cache state). Test what the caller observes.

**Test file layout:**
- Route handler tests: `app/api/**/*.test.ts` — use `NextRequest` mocks
- Component tests: `components/**/*.test.tsx` — use `@testing-library/react`
- Utility tests: `util/**/*.test.ts`

Write a BDD scenario for every new public function, every new route handler, and every UI flow involving routing, login, or raid calendar logic.

## Mandatory Git Workflow

1. Start every task with a clean workspace.
2. If workspace is not clean, stop and alert the user.
3. Work in a dedicated branch: `claude/<short-slug>`.
   - Default branch-creation command: `git switch -c claude/<short-slug>`.
4. Prefer `git switch -c` over `git checkout -b` unless a concrete repo-specific reason requires a different command form.
5. Avoid using `git -C`.
6. Keep changesets small by default:
   - Small commit target: `<= 5` files changed and `<= 250` total changed lines (additions + deletions).
   - Small task-branch target: `<= 30` files changed and `<= 900` total changed lines relative to `main`.
   - Thresholds are a planning and review aid, not a reason to degrade design quality or implementation clarity.
   - Do not force brittle shortcuts (for example embedding large build/config definitions inline solely to reduce file count).
   - Commit each coherent partial finish as soon as it is ready; do not defer finished implementation chunks to a single end-of-task commit.
   - Before branch closure recommendation, run a quick commit-stack review and normalize commit structure (split/squash/reword) when clarity or traceability needs improvement.
   - If projected work exceeds small-task thresholds, split automatically into sequenced subtasks/branches before continuing.
7. Merge strategy is rebase-and-merge.
8. When all branch work is complete, use `superpowers:finishing-a-development-branch` to close.
   Rebase-and-merge is the required strategy; keep workspace clean on `main` after close.
9. Guidance changes based on user policy approvals must be documented in guidance files in the same task.
10. Before claiming work complete or closing a branch, use `superpowers:verification-before-completion`
    to confirm tests pass and the build is clean.
11. For non-trivial tasks, use `superpowers:requesting-code-review` before merging.
12. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add raids route`. Keep commits scoped and direct.
13. Pull request descriptions: explain the change, list any env or schema changes, and include screenshots for UI work.
14. Do not add `Co-Authored-By` trailers to commits. AI usage is acknowledged in `README.md` instead.

## Configuration & Secrets

Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates, and keep local overrides out of version control. Prefer named constants over magic strings; use env vars for environment-specific values.

## DevSecOps & Dependency Security

**Pin exact versions — never use ranges for dependencies.**
Using `^` or `~` silently accepts future versions that may introduce vulnerabilities or supply-chain compromises. Always specify exact versions:

```json
"next": "15.3.9"   ✓
"next": "^15.0.0"  ✗  accepts any future 15.x
```

When adding or upgrading a dependency:
1. Look up the exact latest version via `npm view <pkg> version` or the npm registry — do not guess version numbers.
2. Use context7 or the package changelog to confirm the version exists and is stable before writing it.
3. After updating `package.json`, regenerate the lock file (`npm install`) so `package-lock.json` pins the full transitive tree.
4. Commit `package-lock.json` alongside `package.json` — the lock file is the real supply-chain protection.

**OWASP Controls:**
- **A01 (Broken Access Control):** All user-facing redirects in the Battle.net auth flow are normalised to relative paths to prevent open redirect.
- **A02/A09 (Sensitive Data / Logging):** Never log or expose access tokens, Battle.net credentials, or database passwords.
- **A03 (Injection):** Prisma uses parameterised queries throughout — do not concatenate user input into raw queries.
- **A07 (Auth):** OAuth tokens are stored in HttpOnly cookies — not accessible to JavaScript.

## Data-Driven Development

**Principle:** Configuration belongs in one place, not scattered as repeated literals.

**Named constants for magic strings:**
- **Cookie name** — `battlenet_token` appears in multiple files. Define it once in a constants module and import it everywhere.
- **Route paths** — `/login/success`, `/login/failed`, `/raids` should be defined in a shared constants file, not inlined at each redirect.
- **Cache TTLs, delays, thresholds** — name them at the top of the module (`const CACHE_TTL_MS = 5 * 60 * 1000`) rather than embedding arithmetic inline.

**Config arrays over repeated procedural logic** — canonical example in `app/api/wow/update/route.ts`:

```typescript
const ENTITY_SYNC_DEFS: EntitySyncDef[] = [
  { name: "realms",  fetch: fetchRealms,  upsert: upsertRealm  },
  { name: "classes", fetch: fetchClasses, upsert: upsertClass  },
];
for (const def of ENTITY_SYNC_DEFS) { ... }
```

Adding a new Blizzard entity type = one new entry in the array, no new control flow.

**Environment variables for environment-specific values:**
- Callback URIs, app base URLs — always from `process.env`, never default to localhost strings in production-path code.

**Acceptable hard-coding:**
- Enum values from the Prisma schema (e.g. `RaidVisibility.PUBLIC`) — single source of truth is the schema.
- Framework-required literals (e.g. Next.js `matcher` in `middleware.ts`).
- Test mock URLs like `http://localhost/api/raids` — test-only.

## Using Context7 for Library Documentation

Before non-trivial changes to code that uses an external library, use the Context7 MCP tools to pull up-to-date documentation rather than relying on training-data knowledge.

**Workflow:**
1. Call `mcp__context7__resolve-library-id` with the library name and a short description of what you need.
2. Use the returned library ID to call `mcp__context7__query-docs` with a specific question.

**Key library IDs:**

| Library | Context7 ID |
|---------|-------------|
| Next.js | `/vercel/next.js` |
| Prisma | `/prisma/prisma` |
| MUI v6 | `/mui/material-ui` |
| TanStack Table v8 | `/tanstack/table` |
| Compose Specification | `/compose-spec/compose-spec` |
| axios | `/axios/axios` |
| date-fns | `/date-fns/date-fns` |

Use for: Next.js App Router, Route Handlers, middleware, Server Components; Prisma query builder, relations, migrations; MUI v6 components; TanStack Table v8 API; any version upgrade.

## Gotchas & Critical Patterns

- **Prisma lazy Proxy** — `lib/prisma.ts` wraps PrismaClient in a Proxy so it is never instantiated at import time. This is required for `next build` to succeed when `DATABASE_URL` is absent. Do not remove or simplify this pattern.
- **Prisma 7 driver adapter** — `prisma.config.ts` holds the datasource URL (not `schema.prisma`). PrismaClient is constructed as `new PrismaClient({ adapter: new PrismaPg({ connectionString }) })`. The `driverAdapters` preview feature flag is gone in v7 (now stable).
- **`useSearchParams()` requires `<Suspense>`** — Next.js 15 throws if `useSearchParams` is used outside a Suspense boundary. Always wrap the component.
- **`battlenet.ts` module-level singleton** — `export const battlenet = new BattlenetService()` at module level is safe because the constructor only reads env vars; Prisma is accessed lazily.
- **Auth cookie name** — `battlenet_token` (HttpOnly, not accessible to JS). Defined as a named constant; import it rather than inlining the string.

## Documentation Separation

`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions for the AI agent. Do not mix in user-facing content (setup guides, feature docs, API references for humans). User-facing documentation belongs in `README.md` or a separate `docs/user/` subtree.

Plans and specs live in `docs/superpowers/plans/` and `docs/superpowers/specs/` respectively.
