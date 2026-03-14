# Frontend Guidelines

## Project Structure

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
    constants.ts  ← named constants (cookie name, route paths) — create if absent
  components/     ← ThemeRegistry, NavBar, Logo
  util/           ← ApiUtil, DateUtil
  prisma/
    schema.prisma ← single source of truth for all models
    migrations/
  prisma.config.ts ← Prisma CLI config (datasource url lives here, not schema.prisma)
  middleware.ts   ← protects /raids/* routes
```

## Commands

Set up env files: `cp example.env .env` and `cp frontend/example.env frontend/.env.local`.

- `docker compose up --build`: runs Next.js + PostgreSQL. `docker-compose.override.yml` enables hot-reload dev (port 3001).
- `cd frontend && npm install && npm run dev`: dev server on port 3001.
- `cd frontend && npm run build`: builds with `prisma generate && next build`.
- `cd frontend && npm run lint`: runs ESLint.
- `cd frontend && npm test`: runs Jest / Testing Library.
- `cd frontend && npm test -- --watch`: watch mode.
- `cd frontend && npx prisma migrate dev`: applies pending migrations (requires `DATABASE_URL`).
- `npx playwright test`: E2E tests (requires app running; see `playwright/`).

## Coding Style

TypeScript strict mode throughout. Double quotes, semicolons, match surrounding indentation. `PascalCase` for React components; `camelCase` for variables, functions, Prisma field names. Route Handlers live in `app/api/**/route.ts`.

## Test Strategy (BDD with TDD workflow)

Use the `superpowers:test-driven-development` skill. Project-specific conventions:

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
- React components: use Testing Library user-centric queries (`getByRole`, `getByText`) — never class names or internal state.
- One assertion per scenario; name the scenario in the `it` string.
- Do **not** test implementation details (Prisma method calls, internal cache state).

**Test file layout:**
- Route handler tests: `app/api/**/*.test.ts` — use `NextRequest` mocks
- Component tests: `components/**/*.test.tsx` — use `@testing-library/react`
- Utility tests: `util/**/*.test.ts`

Write a BDD scenario for every new public function, route handler, and UI flow involving routing, login, or raid calendar logic.

## DevSecOps & Dependency Security

**Pin exact versions — never use `^` or `~`.** Always look up the exact latest version via `npm view <pkg> version` before writing it. Regenerate and commit `package-lock.json` alongside `package.json`.

**OWASP Controls:**
- **A01:** Redirects in the Battle.net auth flow use relative paths — no open redirect.
- **A02/A09:** Never log access tokens, Battle.net credentials, or database passwords.
- **A03:** Prisma uses parameterised queries — do not concatenate user input into raw queries.
- **A07:** OAuth tokens stored in HttpOnly cookies — not accessible to JavaScript.

## Data-Driven Development

Configuration belongs in one place, not scattered as repeated literals.

**Named constants** live in `lib/constants.ts` (create if absent): cookie name (`battlenet_token`), route paths (`/login/success`, `/login/failed`, `/raids`), cache TTLs.

**Config arrays over repeated procedural logic** — canonical example in `app/api/wow/update/route.ts`:

```typescript
const ENTITY_SYNC_DEFS: EntitySyncDef[] = [
  { name: "realms",  fetch: fetchRealms,  upsert: upsertRealm  },
  { name: "classes", fetch: fetchClasses, upsert: upsertClass  },
];
for (const def of ENTITY_SYNC_DEFS) { ... }
```

Adding a new Blizzard entity = one new array entry, no new control flow.

**Acceptable hard-coding:** Prisma enum values (e.g. `RaidVisibility.PUBLIC`), Next.js `matcher` literals, test mock URLs.

## Using Context7 for Library Documentation

Before non-trivial changes to external library code, use Context7 MCP tools instead of relying on training-data knowledge:

1. `mcp__context7__resolve-library-id` — get the library ID
2. `mcp__context7__query-docs` — query with a specific question

| Library | Context7 ID |
|---------|-------------|
| Next.js | `/vercel/next.js` |
| Prisma | `/prisma/prisma` |
| MUI v6 | `/mui/material-ui` |
| TanStack Table v8 | `/tanstack/table` |
| Compose Specification | `/compose-spec/compose-spec` |
| axios | `/axios/axios` |
| date-fns | `/date-fns/date-fns` |

## Gotchas & Critical Patterns

- **Prisma lazy Proxy** — `lib/prisma.ts` wraps PrismaClient in a Proxy so it is never instantiated at import time. Required for `next build` without `DATABASE_URL`. Do not simplify or remove.
- **Prisma 7 driver adapter** — `prisma.config.ts` holds the datasource URL (not `schema.prisma`). Constructor: `new PrismaClient({ adapter: new PrismaPg({ connectionString }) })`. The `driverAdapters` preview flag is gone in v7 (now stable).
- **`useSearchParams()` requires `<Suspense>`** — Next.js 15 throws outside a Suspense boundary. Always wrap.
- **`battlenet.ts` module-level singleton** — `export const battlenet = new BattlenetService()` is safe because the constructor only reads env vars; Prisma is accessed lazily.
- **Auth cookie name** — `battlenet_token` (HttpOnly). Import from `lib/constants.ts`; never inline the string.
