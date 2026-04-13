# Extension: .NET — integration-rubric addon

Addon to [dotnet-core.md](dotnet-core.md) loaded **only when step 0b selects the integration rubric**. Carries framework-specific smells and procedures that apply exclusively under the integration rubric:

- Integration-only high-confidence smells (`dotnet.I-HC-*`) that refine core `I-HC-A*` / `I-HC-B*` codes.
- The **auth matrix enumeration** procedure (step 2.6 of the deep-mode workflow) — integration-only because auth enforcement is an HTTP-seam concern.
- The **migration upgrade-path enumeration** procedure (step 2.7) — integration-only because migrations run against a real data store.

**Prerequisite:** `dotnet-core.md` must already be loaded. This file uses the test-type detection signals, test double classification, carve-outs, SUT surface enumeration, determinism verification, and Stryker mutation tool declared there.

The rubric-neutral smells in `dotnet-core.md` (`dotnet.HC-1` through `dotnet.HC-7`, `dotnet.LC-2`/`LC-4`/`LC-6`..`LC-9`, `dotnet.POS-*`) all apply under the integration rubric too — they declare `Applies to: unit, integration`. Do not re-list them here.

---

## Framework-specific high-confidence integration smells (`dotnet.I-HC-*`)

### `dotnet.I-HC-A1` — Shared `WebApplicationFactory<T>` via `IClassFixture<>` with no per-test data scoping

**Applies to:** `integration` — refines core `I-HC-A2` / `I-HC-A4`.

**Detection:** a test class declares `IClassFixture<WebApplicationFactory<TProgram>>` (or a custom factory subclass) and uses the injected factory's `CreateClient()` across multiple test methods without per-test data scoping (unique keys, fresh DB scope, or test-specific `WebApplicationFactoryClientOptions`).

**Smell:** the factory amortizes expensive host construction (which is a positive — see `dotnet.POS-3`) but the data seen by each test is whatever the previous test left behind. Tests pass in isolation and fail in suite, or vice versa.

**Example (smell):**
```csharp
public class OrdersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public OrdersApiTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Post_Order_Creates_Row()
    {
        var response = await _client.PostAsJsonAsync("/orders", new { sku = "A", qty = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Get_Orders_Returns_Seeded_Rows()
    {
        var orders = await _client.GetFromJsonAsync<List<Order>>("/orders");
        orders.Should().HaveCount(1); // depends on the Post test running first
    }
}
```

**Rewrite (intent):** scope data per test. Either generate a unique key per test and assert on it, or use `IAsyncLifetime.InitializeAsync` to create a fresh per-test scope (e.g. a per-test tenant id, a per-test Cosmos partition key, or a Respawn checkpoint reset).

---

### `dotnet.I-HC-B1` — `factory.CreateClient()` against an auth-protected endpoint with no `Authorization` header or `TestAuthHandler`

**Applies to:** `integration` — refines core `I-HC-B7`.

**Detection:** the test calls `factory.CreateClient()` and then exercises an endpoint that uses `[Authorize]` (or equivalent policy) without either (a) adding an `Authorization` header to the request, (b) configuring a `TestAuthHandler` via `factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddAuthentication("Test").AddScheme<...>))`, or (c) asserting on `401`/`403` for a negative case.

**Smell:** the test exercises only the happy path through real middleware but never validates auth behavior. The test will "pass" for any implementation that lets anonymous requests through, including a broken one.

**Example (smell):**
```csharp
[Fact]
public async Task Get_Admin_Returns_Ok()
{
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/admin/users");
    response.StatusCode.Should().Be(HttpStatusCode.OK); // happens to pass because dev auth is permissive
}
```

**Rewrite (intent):** cover the full matrix per `integration-testing.md §5.2 I-HC-B7` — anonymous (expect `401`), valid token (expect `200`), expired token (expect `401`), insufficient scope (expect `403`). Use a `TestAuthHandler` scheme registered via `factory.WithWebHostBuilder` so the test controls exactly which principal is presented.

---

## Auth matrix enumeration

Consumed by [SKILL.md § Auth matrix enumeration](../SKILL.md#auth-matrix-enumeration) — step 2.6 of the deep-mode workflow. Integration-only: auth enforcement matrices target HTTP seams, which live under the integration rubric.

### Protected-endpoint patterns

Enumerate endpoints that require authentication. In .NET isolated Functions and ASP.NET Core:

- **Functions isolated with HttpTrigger:** `\[HttpTrigger\(AuthorizationLevel\.(?P<level>Function|Admin|User|System|Anonymous)` — any level other than `Anonymous` is a protected endpoint. Capture the `Route = "..."` and HTTP methods.
- **Functions with `[Authorize]`:** `using Microsoft.AspNetCore.Authorization;` plus `\[Authorize(\([^)]*\))?\]` on a `[Function(...)]`-decorated class or method.
- **ASP.NET Core MVC / minimal API `[Authorize]`:** `\[Authorize(\([^)]*\))?\]` on a controller class or action method, or `app.MapGet(...).RequireAuthorization(...)` / `.RequireAuthorization("Policy")`.
- **Custom authorization middleware:** any file in the SUT that registers `UseAuthentication()` / `UseAuthorization()` plus a per-route policy on the endpoint registration.

Record scope / policy / role requirements by capturing the `[Authorize(Policy = "...")]` / `[Authorize(Roles = "...")]` argument, or the `.RequireAuthorization("<policy>")` string.

### Auth scenario detection in tests

For each enumerated endpoint, search the test project for each matrix column:

- **`anonymous`** — a test that calls `factory.CreateClient()` (no auth handler configured) and asserts `401 Unauthorized` on the endpoint. Look for `HttpStatusCode.Unauthorized` or `.Should().Be(HttpStatusCode.Unauthorized)` alongside the endpoint route.
- **`token-expired`** — a test that arranges a token with a `ValidTo` / `exp` claim in the past (e.g. `DateTimeOffset.UtcNow.AddMinutes(-5)` fed into a `JwtSecurityTokenHandler` or test-auth-handler factory).
- **`token-tampered`** — a test that arranges a valid-format token whose signing key differs from the SUT's configuration, or a token whose `alg` is `none`, asserting `401`.
- **`insufficient-scope`** — a test that uses a `TestAuthHandler` to present a principal *without* the required policy / role and asserts `403 Forbidden`.
- **`sufficient-scope`** — a test that presents a principal with the required policy / role and asserts the documented success code.
- **`cross-user`** — a test that presents user A's principal against a resource created by user B (e.g. a Cosmos partition key owned by user B) and asserts `403 Forbidden` or `404 Not Found`.

### Carve-outs

- **Policy-less `[Authorize]`** — an endpoint decorated with bare `[Authorize]` has no scope / role requirement. The `insufficient-scope` cell is `n/a` for that endpoint; do not flag it as a gap.
- **Single-user product** — if the repo has no multi-tenant model (no per-user resource ownership), the `cross-user` cell is `n/a`. Detect by searching for `partitionKey` / `userId` / `tenantId` parameters on the endpoint; if none, mark `n/a`.
- **`dotnet.I-HC-B1` already fires** — a test flagged under `dotnet.I-HC-B1` (happy-path-only against a `factory.CreateClient()`) is the same gap as the auth matrix rows. Emit one finding per endpoint, not one per test; reference both codes.

---

## Migration upgrade-path enumeration

Consumed by [SKILL.md § Migration upgrade-path enumeration](../SKILL.md#migration-upgrade-path-enumeration) — step 2.7 of the deep-mode workflow. Integration-only: migrations run against a real data store.

### Migration enumeration pattern

For this repo and similar layouts:

- **File glob:** `api/Migrations/*.cs` (or the equivalent subdirectory for the repo).
- **Class detection:** any class whose file is under `api/Migrations/` *and* whose declaration matches `public (sealed )?class [A-Z][A-Za-z0-9_]*Migration` — the repo convention is the `Migration` suffix. Alternatively, the class inherits from a migration base type: `: \s*(IAsync)?Migration\b` / `: \s*MigrationBase\b`.
- **Identifier:** the class name. Migration file names in this repo use a `NNNN_<snake_case>.cs` convention; use the class name, not the file prefix, as the identifier because tests reference the class.

### Upgrade-path test detection

For each enumerated migration, search the test project for a test method that satisfies **all three** conditions:

1. **References the migration class name.** Either `new OrderStatusMigration()` construction or `typeof(OrderStatusMigration)` reference.
2. **Arranges non-empty seed data before the migration runs.** Search the test method's Arrange block for at least one `CreateItemAsync(...)` / `InsertAsync(...)` / `.AddAsync(...)` / `SeedAsync(...)` call on a Cosmos / Entity Framework / equivalent data store, *before* the migration is invoked.
3. **Asserts post-migration state.** After the migration invocation, the test reads rows back (`GetItemAsync`, `ReadItemAsync`, `QueryAsync`, etc.) and asserts a property of the returned data.

If all three conditions fail, emit `Gap-MigUpgrade`. If condition 1 holds but 2 or 3 fail, emit `Gap-MigUpgrade` with a sharper note (e.g. "test exists but arranges no seed data — see `I-HC-A7`").

### Repo-specific carve-outs

- **Migrations runner test.** This repo runs migrations via a `MigrationRunner` in `api/Migrations/MigrationRunner.cs`. A single runner test that invokes the runner with seed data covers every migration class *if* the test explicitly asserts post-state for each class in the assertion block. Detect by searching for a test that references `MigrationRunner` and has an assertion block that names each migration class. When detected, suppress `Gap-MigUpgrade` for the covered migrations and note "covered transitively via `MigrationRunner` test".
- **Migrations container tracking.** `CLAUDE.md` documents that migrations are tracked via a migrations container in Cosmos; each runs at most once. A test that verifies this tracking (e.g. "running MigrationRunner twice applies each migration only once") is a valuable positive (`I-POS-2`) but is **not** a substitute for per-migration upgrade-path tests — flag it separately.
