# LFM

LFM has three parts:

- `app/`: a Blazor WebAssembly single-page app
- `api/`: an Azure Functions backend (.NET 10 isolated)
- `infra/`: Azure infrastructure defined with Bicep

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (version pinned in `global.json`)
- [Docker](https://www.docker.com/) (for E2E tests and local stack)

## Run Locally

### Start local stack

```bash
docker compose -f docker-compose.local.yml up
```

This starts:

- Cosmos emulator on `https://127.0.0.1:8081`
- Azurite blob storage on `http://127.0.0.1:10000`
- Azure Functions on `http://localhost:7071`

### Run the Blazor app

```bash
dotnet run --project app/Lfm.App.csproj
```

Copy `example.env` and set your local overrides. Key values: Blizzard OAuth credentials (`LFM_CLIENT_ID`, `LFM_CLIENT_SECRET`), `HMAC_SECRET` (64 hex chars via `openssl rand -hex 32`), and `APP_BASE_URL`.

## Build and Test

```bash
dotnet restore lfm.sln
dotnet build lfm.sln -c Release
dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release
dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release
```

Format check:

```bash
dotnet format lfm.sln --verify-no-changes --no-restore --severity error
```

Bundle size gate (after publish):

```bash
dotnet publish app/Lfm.App.csproj -c Release -o ./publish/app
./scripts/check-bundle-size.sh ./publish/app/wwwroot 5
```

## E2E Tests

```bash
dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter "Category!=Perf"
```

Requires the Docker test stack from `docker-compose.test.yml`.

## Structure

- `app/`: Blazor WASM pages, components, services
- `api/Functions/`: Azure Function handlers
- `api/Migrations/`: database migration scripts
- `shared/`: shared models and contracts used by both app and api
- `tests/Lfm.Api.Tests/`: xUnit tests for API handlers
- `tests/Lfm.App.Tests/`: bUnit component tests for Blazor
- `tests/Lfm.E2E/`: Playwright .NET end-to-end tests
- `infra/main.bicep`: main infrastructure entry point

## Notes

Use standard C# conventions: 4-space indentation. Run `dotnet format` before committing. Keep commits short and imperative.

## AI Assistance

This project is developed with the assistance of [Claude Code](https://claude.ai/claude-code) and [Codex](https://openai.com/codex/). AI is used for coding, code review, and documentation tasks throughout the codebase.
