# LFM

LFM has three parts:

- `frontend/`: a Vite + React single-page app
- `functions/`: an Azure Functions backend
- `infra/`: Azure infrastructure defined with Bicep

## Prerequisites

This project uses [fnm](https://github.com/Schniz/fnm) (Fast Node Manager) to pin the Node.js version (see `.node-version`) and **pnpm** as the package manager (provided via corepack). Always run Node/pnpm commands through `fnm exec`:

```bash
fnm exec pnpm -C frontend install
fnm exec pnpm -C functions install
```

## Run Locally

### Preferred runner

```bash
./scripts/dev-env.mjs serve
```

This starts the local dev stack with:

- Vite on `http://localhost:5173`
- Azure Functions in Docker on `http://localhost:7071`
- Cosmos emulator on `http://127.0.0.1:8081`
- Azurite blob storage on `http://127.0.0.1:10000`

`serve` uses real local auth/secrets from the repo-root `.env`, reuses the live WoW reference-data cache behavior, and keeps its local state isolated under `.tmp/dev/`.

Useful companion commands:

- `./scripts/dev-env.mjs test`
- `./scripts/dev-env.mjs test runs-error`
- `./scripts/dev-env.mjs test signup`
- `./scripts/e2e-all.sh`
- `./scripts/dev-env.mjs refresh-reference`
- `./scripts/dev-env.mjs reset`
- `./scripts/dev-env.mjs down`

`./scripts/e2e.sh ...` remains available as a compatibility wrapper for `./scripts/dev-env.mjs test ...`.

`./scripts/e2e-all.sh` is the intended full e2e suite. It runs the default Playwright-discovered specs plus the scenario-specific specs that require separate seed states.

Default Playwright discovery currently runs with `workers: 1`. The local Docker-backed test stack seeds a shared database, so serial execution is intentional until each spec gets fully isolated state.

The `test` runner uses a separate stack on different ports (`4173`, `7072`, `8082`, `10001`) and separate scratch/data paths under `.tmp/e2e/`, so local dev and e2e can run at the same time without mixing data.

Copy `example.env` before using `serve`. The runner overrides local emulator URLs automatically, so the important values in `.env` are your Blizzard credentials plus `SESSION_ENCRYPTION_KEY` and `HMAC_SECRET`.

### Frontend

```bash
fnm exec pnpm -C frontend install
fnm exec pnpm -C frontend dev
```

### Backend

```bash
fnm exec pnpm -C functions install
fnm exec pnpm -C functions build
fnm exec pnpm -C functions start
```

Useful checks:

- `fnm exec pnpm -C frontend build`
- `fnm exec pnpm -C functions build`
- `fnm exec pnpm -C functions test`

## Local Verification

Use the repo-level verifier to keep the quality bar explicit:

- `./scripts/verify-local.sh fast`
  - backend build + tests
  - frontend lint + unit tests + build + bundle budget gate
- `./scripts/verify-local.sh browser`
  - everything in `fast`
  - full Playwright journey suite
- `./scripts/verify-local.sh full`
  - everything in `browser`
  - frontend Playwright perf suite

Use `fast` for ordinary local iterations, `browser` for user-flow changes, and `full` as the final integrated verifier for this program.

Copy `example.env` and `frontend/example.env` before running locally. Do not commit populated `.env` files.

## Structure

- `frontend/src/features`: feature modules (`auth`, `characters`, `guild`, `runs`)
- `frontend/src/components`: shared UI components
- `frontend/src/lib`: shared frontend logic
- `functions/src/functions`: Azure Function handlers
- `functions/src/lib`: shared backend helpers
- `infra/main.bicep`: main infrastructure entry point

Register new backend functions in `functions/src/index.ts` so Azure Functions can discover them.

## Notes

Use the existing TypeScript style in the repo: 2-space indentation, double quotes, and semicolons. Keep commits short and imperative.

## AI Assistance

This project is developed with the assistance of [Claude Code](https://claude.ai/claude-code) and [Codex](https://openai.com/codex/). AI is used for coding, code review, and documentation tasks throughout the codebase.
