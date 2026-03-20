# sisu-raidcal

Sisu Raid Calendar has three parts:

- `frontend/`: a Vite + React single-page app
- `functions/`: an Azure Functions backend
- `infra/`: Azure infrastructure defined with Bicep

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
- `./scripts/dev-env.mjs test raids-error`
- `./scripts/dev-env.mjs test signup`
- `./scripts/dev-env.mjs refresh-reference`
- `./scripts/dev-env.mjs reset`
- `./scripts/dev-env.mjs down`

`./scripts/e2e.sh ...` remains available as a compatibility wrapper for `./scripts/dev-env.mjs test ...`.

The `test` runner uses a separate stack on different ports (`4173`, `7072`, `8082`, `10001`) and separate scratch/data paths under `.tmp/e2e/`, so local dev and e2e can run at the same time without mixing data.

Copy `example.env` before using `serve`. The runner overrides local emulator URLs automatically, so the important values in `.env` are your Blizzard credentials plus `TOKEN_ENCRYPTION_KEY` and `HMAC_SECRET`.

### Frontend

```bash
cd frontend
npm ci
npm run dev
```

### Backend

```bash
cd functions
npm ci
npm run build
npm start
```

Useful checks:

- `cd frontend && npm run build`
- `cd functions && npm run build`
- `cd functions && npm test`

Copy `example.env` and `frontend/example.env` before running locally. Do not commit populated `.env` files.

## Structure

- `frontend/src/components`: shared UI
- `frontend/src/pages`: route pages
- `frontend/src/lib`: shared frontend logic
- `functions/src/functions`: Azure Function handlers
- `functions/src/lib`: shared backend helpers
- `infra/main.bicep`: main infrastructure entry point

Register new backend functions in `functions/src/index.ts` so Azure Functions can discover them.

## Notes

Use the existing TypeScript style in the repo: 2-space indentation, double quotes, and semicolons. Keep commits short and imperative.

## AI Assistance

This project is developed with the assistance of [Claude Code](https://claude.ai/claude-code) and [Codex](https://openai.com/codex/). AI is used for coding, code review, and documentation tasks throughout the codebase.
