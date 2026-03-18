# Repository Guidelines

## Project Structure & Module Organization
This repo is split into three deployable areas:
- `frontend/`: Vite + React SPA. Main code lives in `frontend/src/`, with UI in `components/`, route pages in `pages/`, shared logic in `lib/`, and global styles in `styles/`.
- `functions/`: Azure Functions backend. HTTP handlers live in `functions/src/functions/`, shared helpers in `functions/src/lib/`, middleware in `functions/src/middleware/`, scripts in `functions/src/scripts/`, and types in `functions/src/types/`.
- `infra/`: Azure infrastructure as code with `main.bicep` and reusable modules under `infra/modules/`.

## Build, Test, and Development Commands
Run commands from the relevant package directory:
- `cd frontend && npm ci && npm run dev`: start the SPA locally with Vite.
- `cd frontend && npm run build`: type-check and build the frontend bundle.
- `cd functions && npm ci && npm run build`: compile the Azure Functions TypeScript project.
- `cd functions && npm start`: run the backend locally with Azure Functions Core Tools.
- `cd functions && npm test`: run backend unit tests with Vitest.

## Coding Style & Naming Conventions
Use TypeScript with 2-space indentation, double quotes, and semicolons to match the existing code. Prefer `PascalCase` for React components and page files (`RaidDetailPage.tsx`), and `camelCase` for utilities and config modules (`attendanceConfig.ts`). Keep function handlers focused and register any new backend entry point in `functions/src/index.ts` so the runtime discovers it. No dedicated formatter or linter is configured; use `tsc --noEmit` and existing file style as the baseline.

## Testing Guidelines
Backend tests use Vitest and live beside the code as `*.test.ts`, for example `functions/src/lib/cache.test.ts`. Add or update tests when changing shared backend logic or request validation. Frontend currently has no automated test suite, so at minimum verify affected flows manually and ensure `npm run build` succeeds.

## Commit & Pull Request Guidelines
Recent history uses short, imperative commit subjects such as `Add RaidInfoCard component` and `Update signup handler`. Follow that pattern and keep changes focused. PRs should explain the user-visible change, call out any env or schema updates, and include screenshots for frontend work. If you touch secrets or local config, use `example.env` as the template and never commit populated `.env` files.
