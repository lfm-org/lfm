# Testing guide

Three-lane test strategy. Each lane has a single purpose — do not blur the lines.

| Lane | Runner | Purpose | Location |
|---|---|---|---|
| Unit | xUnit | Pure logic, mocked dependencies, fast feedback | `tests/Lfm.Api.Tests/`, `tests/Lfm.App.Tests/` |
| Component | bUnit | Blazor component rendering, interactions | `tests/Lfm.App.Tests/` (alongside unit) |
| E2E | Playwright .NET | Auth flows, multi-step journeys | `tests/Lfm.E2E/` |

## Unit test invariants

1. **Always deterministic.** Never `Thread.Sleep` or `Task.Delay`. Use `TaskCompletionSource` to represent pending state in bUnit.
2. **Always fast.** The full unit suite (Api + App) must run in under 2 seconds. If a new test crosses that budget, move it to E2E or decompose the scenario.
3. **Always isolated.** Every test constructs its own SUT and mocks — no shared state across tests. xUnit class parallelism is enabled via `xunit.runner.json`; do not introduce `[Collection(...)]` fixtures without strong justification.
4. **Parallel-safe by default.** Run both projects concurrently in scripts with `dotnet test ... & dotnet test ... & wait`.

## Logging assertions — use TestLogger<T>, not Moq

Do NOT assert on log messages via Moq's `Log.Verify(v.ToString()!.Contains(...))`. That pattern couples tests to log wording. Instead use `TestLogger<T>` (in `tests/Lfm.Api.Tests/TestLogger.cs`):

```csharp
var logger = new TestLogger<RunsSignupFunction>();
var fn = new RunsSignupFunction(runsRepo.Object, raidersRepo.Object, permissions.Object, logger);

await fn.Run(req, "run-1", ctx, CancellationToken.None);

logger.Entries.Should().ContainSingle(e => e.IsAudit(
    action: "signup.create",
    actorId: "bnet-user",
    result: "success"));
```

For free-form log messages, assert on `e.Message.Contains(...)` or on named properties directly via `e.Properties["MyProp"]`.

## Repository-layer tests

Cosmos SDK is painful to mock. `RunsRepositoryConcurrencyTests` is the exception that proves the rule — it exists only to pin etag semantics. All other repository behavior is intentionally covered by the E2E stack (which runs the real Cosmos emulator). When adding new repository code, prefer an E2E spec over a heavily-mocked unit test.

## App-side service tests

Prefer unit tests that use `StubHttpMessageHandler` (in `tests/Lfm.App.Tests/Services/`) to cover HTTP serialization, status-code handling, and URL construction. See `RunsClientTests.cs` as the canonical template for adding client tests.

## Coverage

Coverlet is wired into both unit projects. Collect with:

```bash
dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .cache/coverage/api
dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --collect:"XPlat Code Coverage" --results-directory .cache/coverage/app
```

Baseline at the time this guide was written (2026-04-10):
- `Lfm.Api.Tests` line coverage: `47.0%`
- `Lfm.App.Tests` line coverage: `65.5%`

Coverage is **visibility only — not a CI gate.** Low coverage on a well-tested critical path is fine; high coverage on a trivial DTO is meaningless. Chase value, not percentage.

## Running tests in parallel

```bash
dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release & \
dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release & \
wait
```

Cross-class parallelism inside each assembly is enabled via `xunit.runner.json` in each project's root.
