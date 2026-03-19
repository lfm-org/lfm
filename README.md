# sisu-raidcal

Sisu Raid Calendar has three parts:

- `frontend/`: a Vite + React single-page app
- `functions/`: an Azure Functions backend
- `infra/`: Azure infrastructure defined with Bicep

## Run Locally

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
