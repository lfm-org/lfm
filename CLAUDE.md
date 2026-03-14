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
@docs/TESTING.md

## Mandatory Git Workflow
Start clean, work in a `claude/<short-slug>` branch, keep changesets small, and rebase-and-merge to `main`.
Do not add `Co-Authored-By` trailers to commits. AI usage is acknowledged in `README.md` instead.
@docs/GIT.md

## Configuration & Secrets
Never commit populated `.env` files or credentials. Prefer named constants over magic strings; use env vars for environment-specific values.
@docs/SECURITY.md
@docs/DATA_DRIVEN.md

## DevSecOps & Dependency Security
Pin exact dependency versions (never `^` or `~`). Follow OWASP A01–A09 controls.
@docs/SECURITY.md

## Data-Driven Development
Prefer config arrays and named constants over scattered magic strings and repeated literals.
@docs/DATA_DRIVEN.md

## Using Context7 for Library Documentation
Before non-trivial changes to an external library, pull docs via Context7 MCP.
@docs/CONTEXT7.md

## Documentation Separation
`CLAUDE.md` and `docs/` are Claude-facing: guidance, workflow rules, and architectural decisions for the AI agent. Do not mix in user-facing content (setup guides, feature docs, API references for humans). User-facing documentation belongs in `README.md` or a separate `docs/user/` subtree.
