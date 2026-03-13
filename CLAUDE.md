# Repository Guidelines

## Project Structure & Module Organization
This repository is split into `backend/` and `frontend/`. The backend is a NestJS API: feature modules live under `backend/src/` in folders such as `auth/`, `characters/`, `raiders/`, `raids/`, and `wow/`, with DTOs and entities kept near each feature. End-to-end tests live in `backend/test/`. The frontend is a React + TypeScript app in `frontend/src/`; UI lives in `components/`, state in `models/`, helpers in `util/`, and assets in `frontend/public/`. Root `docker-compose.yml` wires the stack with PostgreSQL.

## Build, Test, and Development Commands
Set up env files: `cp example.env .env`, `cp backend/example.env backend/.env`, and `cp frontend/example.env frontend/.env`.

- `podman compose up --build`: runs frontend, backend, and PostgreSQL together. `docker-compose.override.yml` is applied automatically for hot-reload dev mode (frontend on port 3001, backend on port 3000).
- `cd backend && npm install && npm run start:dev`: starts the Nest API with file watching.
- `cd frontend && npm install && npm start`: starts the React dev server.
- `cd backend && npm run build`: builds the API into `backend/dist/`.
- `cd backend && npm test` or `npm run test:e2e`: runs unit or e2e Jest suites.
- `cd frontend && npm test`: runs the CRA/Jest test runner.

## Coding Style & Naming Conventions
Use TypeScript throughout. Keep double quotes and semicolons, and follow the surrounding file's indentation instead of reformatting unrelated lines. Backend lint rules are defined in `backend/.eslintrc.js`; ESLint and Prettier packages exist in both apps. Use `PascalCase` for React components, Nest modules, DTOs, and entities; use `camelCase` for variables, functions, and store fields. Keep Nest filenames role-based, for example `raiders.service.ts`.

## Testing Guidelines
Backend unit tests follow Nest/Jest defaults with `*.spec.ts`; e2e tests live in `backend/test/` as `*.e2e-spec.ts`. Frontend tests run through `react-scripts` with Testing Library setup in `frontend/src/setupTests.ts`. No coverage threshold is enforced, so add focused tests for new controller or service behavior and UI flows involving routing, login, or raid calendar logic.

## Commit & Pull Request Guidelines
Recent commits use short, imperative subjects such as `Fix docker` and `Google Login`. Keep commits scoped and similarly direct. Pull requests should explain the change, note which app (`frontend`, `backend`, or both) was touched, list env or schema changes, and include screenshots for UI work. Add the commands you ran to verify the change before requesting review.

## Configuration & Secrets
Do not commit populated `.env` files or real Blizzard or database credentials. Use the checked-in `example.env` files as templates, and keep local overrides out of version control.

## Using Context7 for Library Documentation
Before making non-trivial changes to code that uses an external library, use the Context7 MCP tools to pull up-to-date documentation rather than relying on training-data knowledge.

**Workflow:**
1. Call `mcp__context7__resolve-library-id` with the library name and a short description of what you need.
2. Use the returned library ID to call `mcp__context7__query-docs` with a specific question.

**Key library IDs for this project:**

| Library | Context7 ID |
|---------|-------------|
| NestJS | `/nestjs/docs.nestjs.com` |
| NestJS (source/versions) | `/nestjs/nest` |
| Podman | `/containers/podman` |
| podman-compose | `/containers/podman-compose` |
| Compose Specification | `/compose-spec/compose-spec` |

**When to use it:**
- NestJS guards, modules, decorators, lifecycle hooks
- TypeORM query builder, relations, migrations
- Podman/podman-compose volume options, `userns_mode`, `x-podman` extensions
- React Router, Material-UI, or any other frontend dependency
- Any time a version upgrade is involved — check the changelog via context7 first
