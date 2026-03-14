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
    prisma.ts     ← Prisma client singleton
    battlenet.ts  ← OAuth2 logic (no DI, plain module)
    auth.ts       ← requireAuth helper
  components/     ← ThemeRegistry, NavBar, Logo
  util/           ← ApiUtil, DateUtil
  prisma/
    schema.prisma ← single source of truth for all models
    migrations/
  middleware.ts   ← protects /raids/* routes
```

Root `docker-compose.yml` has 2 services: `frontend` (Next.js) + `database` (PostgreSQL).

## Build, Test, and Development Commands
Set up env files: `cp example.env .env` and `cp frontend/example.env frontend/.env.local`.

- `docker compose up --build`: runs Next.js and PostgreSQL. `docker-compose.override.yml` enables hot-reload dev (port 3001).
- `cd frontend && npm install && npm run dev`: starts Next.js dev server on port 3001.
- `cd frontend && npm run build`: builds with `prisma generate && next build`.
- `cd frontend && npm test`: runs Jest / Testing Library.

## Coding Style & Naming Conventions
Use TypeScript throughout with strict mode. Keep double quotes and semicolons, follow the surrounding file's indentation. Use `PascalCase` for React components; `camelCase` for variables, functions, and Prisma field names. Next.js Route Handlers live in `app/api/**/route.ts`.

## Behavior-Driven Development (BDD)

All tests should describe **observable behavior**, not implementation internals.

**Naming pattern** — use `Given / When / Then` in test descriptions:
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

## Testing Guidelines
Tests run via Jest. Add focused tests for new route handler or service behavior and for UI flows involving routing, login, or raid calendar logic. No coverage threshold is enforced today, but every new public function should have at least one BDD-style scenario.

## Commit & Pull Request Guidelines
Recent commits use short, imperative subjects such as `Fix docker` and `Add raids route`. Keep commits scoped and similarly direct. Pull requests should explain the change, list env or schema changes, and include screenshots for UI work.

## Configuration & Secrets
Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates, and keep local overrides out of version control.

## DevSecOps & Dependency Security (OWASP A06)

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

**Other OWASP considerations for this codebase:**
- Never log or expose access tokens, Battle.net credentials, or database passwords (OWASP A02/A09).
- OAuth tokens are stored in HttpOnly cookies — not accessible to JavaScript (OWASP A07).
- All user-facing redirects in the Battle.net auth flow are normalised to relative paths to prevent open redirect (OWASP A01).
- Prisma uses parameterised queries throughout — do not concatenate user input into raw queries (OWASP A03).

## Using Context7 for Library Documentation
Before making non-trivial changes to code that uses an external library, use the Context7 MCP tools to pull up-to-date documentation rather than relying on training-data knowledge.

**Workflow:**
1. Call `mcp__context7__resolve-library-id` with the library name and a short description of what you need.
2. Use the returned library ID to call `mcp__context7__query-docs` with a specific question.

**Key library IDs for this project:**

| Library | Context7 ID |
|---------|-------------|
| Next.js | `/vercel/next.js` |
| Prisma | `/prisma/prisma` |
| MUI v6 | `/mui/material-ui` |
| TanStack Table v8 | `/tanstack/table` |
| Podman | `/containers/podman` |
| Compose Specification | `/compose-spec/compose-spec` |

**When to use it:**
- Next.js App Router, Route Handlers, middleware, Server Components
- Prisma query builder, relations, migrations, upsert patterns
- MUI v6 components, ThemeRegistry, emotion cache
- TanStack Table v8 API (`useReactTable`, `createColumnHelper`, `flexRender`)
- Any time a version upgrade is involved — check the changelog via context7 first
