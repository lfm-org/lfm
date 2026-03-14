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
All tests use Given/When/Then naming and test observable behavior, not implementation internals. Testing is test-first: write a failing scenario before writing implementation code.
→ See [docs/TESTING.md](docs/TESTING.md)

## Mandatory Git Workflow
1. Start every task with a clean workspace.
2. If workspace is not clean, stop and alert the user.
3. Work in a dedicated branch: `claude/<short-slug>`.
4. Keep changesets small by default (≤ 5 files / ≤ 250 lines per commit; ≤ 30 files / ≤ 900 lines per branch).
5. Merge strategy is rebase-and-merge.
6. Branch closure policy: Claude may close task branches without separate user approval once prerequisites are satisfied.
7. Guidance changes based on user policy approvals must be documented in guidance files in the same task.
8. Close branch and return to clean `main` for the next task.
9. Commit message style: short, imperative subjects — e.g. `Fix docker`, `Add raids route`. Keep commits scoped and direct.
10. Pull request descriptions: explain the change, list any env or schema changes, and include screenshots for UI work.
→ See [docs/GIT.md](docs/GIT.md) for full detail including branch-creation commands and changeset guidance.

## Configuration & Secrets
Never commit populated `.env` files or credentials. Prefer named constants over magic strings; use env vars for environment-specific values.
→ See [docs/SECURITY.md](docs/SECURITY.md) and [docs/DATA_DRIVEN.md](docs/DATA_DRIVEN.md)

## DevSecOps & Dependency Security
Pin exact dependency versions (never `^` or `~`). Follow OWASP A01–A09 controls.
→ See [docs/SECURITY.md](docs/SECURITY.md)

## Data-Driven Development
Prefer config arrays and named constants over scattered magic strings and repeated literals.
→ See [docs/DATA_DRIVEN.md](docs/DATA_DRIVEN.md)

## Using Context7 for Library Documentation
Before non-trivial changes to an external library, pull docs via Context7 MCP.
→ See [docs/CONTEXT7.md](docs/CONTEXT7.md)
