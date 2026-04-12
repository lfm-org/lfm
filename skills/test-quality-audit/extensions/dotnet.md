# Extension: .NET

Covers .NET unit, integration, and E2E test stacks: xUnit, NUnit, MSTest, bUnit (Blazor component testing), Playwright .NET (browser-driven E2E), Selenium.WebDriver, and the commonly-used mocking and assertion libraries (Moq, NSubstitute, FakeItEasy, FluentAssertions).

## Detection signals

Load this extension when the audit target contains any of:

- `*.csproj` or `*.sln` files.
- A `.cs` file with `using Xunit;` / `using NUnit.Framework;` / `using Microsoft.VisualStudio.TestTools.UnitTesting;` / `using Bunit;` / `using Microsoft.Playwright;` / `using OpenQA.Selenium;`.
- A `.csproj` with `<PackageReference Include="xunit"` / `"nunit"` / `"MSTest.TestAdapter"` / `"bunit"` / `"Moq"` / `"NSubstitute"` / `"FakeItEasy"` / `"FluentAssertions"` / `"Microsoft.Playwright"` / `"Selenium.WebDriver"` / `"Testcontainers"`.
- A `global.json` or `dotnet-tools.json` in the target tree.

Detection glob shortcuts: `**/*.csproj`, `**/*Tests.cs`, `**/*Tests/*.cs`, `**/Tests/**/*.cs`.

---

## Test type detection signals

Consumed by [SKILL.md § 0b (Rubric selection)](../SKILL.md#0b-select-the-rubric). Declares which patterns route a .NET test to the integration rubric instead of the unit rubric. A test with no matching integration signal defaults to the unit rubric — explicit and backwards compatible.

### Integration rubric signals

Route the test (or the containing file / project) to the integration rubric when any of these are present:

- **Project-level.** Project name matches `*Integration*.Tests*`, OR the project's `<ProjectReference>` transitive closure contains a project using the ASP.NET Core web SDK (`Microsoft.NET.Sdk.Web`).
- **Using directive.** `using Microsoft.AspNetCore.Mvc.Testing;` — imports `WebApplicationFactory<T>`.
- **Construction.** The test constructs or injects any of: `WebApplicationFactory<T>`, `HostBuilder`, `IHostBuilder`, `TestServer`, `DistributedApplicationTestingBuilder` (.NET Aspire), or obtains an `HttpClient` via `factory.CreateClient()`.
- **Real infrastructure helpers.** `using Testcontainers.*;`, `using WireMock.Server;`, Respawn for per-test cleanup, or a similar helper that spins up a real adjacent dependency.
- **Emulator endpoints.** A `CosmosClient` / `BlobServiceClient` / `QueueClient` / equivalent constructed against a local emulator endpoint (`https://localhost:8081` for the Cosmos emulator, `http://127.0.0.1:10000` for Azurite, etc.) rather than mocked.

### Unit rubric signals (default)

Route to the unit rubric (the default) when:

- The test instantiates the SUT directly (`new OrderService(mockRepo.Object, ...)`) with `Mock<T>` / `Substitute.For<T>` / `A.Fake<T>()` dependencies, and
- The file does not import or construct any of the integration-rubric markers above.

### E2E rubric signals

Route the test (or the containing file / project) to the E2E rubric when any of these are present:

- **Project-level.** Project name matches `*E2E*` or `*EndToEnd*`, OR the `.csproj` contains a `<PackageReference>` to `Microsoft.Playwright`, `Microsoft.Playwright.NUnit`, `Microsoft.Playwright.MSTest`, `Microsoft.Playwright.Xunit`, or `Selenium.WebDriver`.
- **Using directive.** `using Microsoft.Playwright;`, `using Microsoft.Playwright.NUnit;`, `using Microsoft.Playwright.MSTest;`, `using OpenQA.Selenium;`, or `using OpenQA.Selenium.Chrome;`.
- **Construction.** The test injects or constructs an `IPlaywright`, `IBrowser`, `IBrowserContext`, `IPage`, `IWebDriver`, or similar browser-session type.
- **Base class or helper.** The test class inherits from `PageTest`, `ContextTest`, `BrowserTest` (Playwright NUnit/MSTest/Xunit base classes), or equivalent project-specific bases that expose a browser session.

Once a file is routed to E2E, classify each test into a sub-lane (`F` functional / `A` accessibility / `P` performance / `S` security) using the sub-lane signals in [SKILL.md § 0b step 5](../SKILL.md#0b-select-the-rubric):

- `[Trait("Category", "Accessibility")]` or axe / `AxeBuilder` / `AccessibilityHelper`-style imports → sub-lane **A**.
- `[Trait("Category", "Perf")]` or Web Vitals / `PerformanceObserver` / `PerfHelper`-style imports → sub-lane **P**.
- `[Trait("Category", "Security")]` or assertions on CSP / cookie jar / cross-origin iframe / tampered-cookie behaviour → sub-lane **S**.
- Otherwise → sub-lane **F**.

### Mixed-file handling

When a single test class contains multiple patterns — some tests use only mocked dependencies, some construct `WebApplicationFactory<T>`, some drive a browser via `IPage` — classify each test method individually. A test is unit, integration, *or* E2E under exactly one rubric; never more than one. The audit records the chosen rubric (and, for E2E, the sub-lane) per test so the reader can audit the dispatch itself.

---

## Test double classification

Required reading for auditors: [../../../docs/quality-reference/unit-testing.md § 7.1](../../../docs/quality-reference/unit-testing.md) — the Fowler taxonomy (Dummy / Stub / Spy / Mock / Fake) that core smells like `HC-5` and `HC-6` are scoped to.

Moq, NSubstitute, FakeItEasy, and Microsoft.Extensions.Logging.Testing all produce test doubles through one construction syntax but serve different roles in the taxonomy. Classify each double before applying interaction-pinning smells:

### Moq

- **Stub:** `new Mock<T>()` (or `Mock.Of<T>(...)`) plus only `.Setup(...)` / `.SetupGet(...)` / `.Returns(...)` / `.ReturnsAsync(...)`, with `mock.Object` passed to the SUT. **No `.Verify(...)` call anywhere in the test body.**
- **Mock (behavior verification):** any `.Verify(...)` / `.VerifyAll()` / `.VerifyNoOtherCalls()` / `.VerifySet(...)` on the double. This is the lens under which `HC-5`, `HC-6`, `dotnet.HC-1` apply.
- **Strict mock:** `new Mock<T>(MockBehavior.Strict)` — every call must be pre-setup; unspecified calls throw. Always a mock for taxonomy purposes.

### NSubstitute

- **Stub:** `Substitute.For<T>()` plus only `.Returns(...)` / `.ReturnsForAnyArgs(...)` / `.ReturnsNull()`, no `Received` call.
- **Mock:** any `.Received(...)` / `.ReceivedWithAnyArgs(...)` / `.DidNotReceive(...)` / `.DidNotReceiveWithAnyArgs(...)` call.

### FakeItEasy

- **Stub:** `A.Fake<T>()` plus only `A.CallTo(() => ...).Returns(...)` / `.ReturnsNextFromSequence(...)`.
- **Mock:** any `A.CallTo(() => ...).MustHaveHappened(...)` / `.MustNotHaveHappened()` / `.MustHaveHappenedOnceExactly()`.

### Fakes (working implementations)

Types named `Fake*`, `InMemory*`, `TestLogger<T>`, `FakeLogger` (Microsoft.Extensions.Logging.Testing), `CapturingLogger`, `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing), or any custom class that implements the real interface with a recording / in-memory / shortcut body are Fowler **fakes**, not mocks. Positive signals: `dotnet.POS-5` (capture logger), `dotnet.POS-6` (FakeTimeProvider). Do not apply `HC-5` / `HC-6` / `dotnet.HC-1` to fakes.

### Interpretation rules

- **Mixed use in one test.** If a test body constructs a `Mock<T>` that is treated as a stub (no `.Verify`) *and* another `Mock<U>` that is verified (mock), classify each double independently. Smells like `HC-5` apply only to the mocked collaborator.
- **One mock per finding.** If a test has three mock collaborators and only one is over-verified, the finding names the offending collaborator rather than marking the entire test as `HC-6`.
- **Same-module owned types.** `dotnet.HC-4` (mocking an owned concrete class) applies regardless of stub-vs-mock classification — the construction of a double against an owned concrete class is the smell, not the verification mode.
- **Heavy `It.IsAny<T>()` in `Setup`.** `dotnet.LC-1` applies when the double is used as a stub — that's the case where `Setup` is the entire contract. A mock with `It.IsAny<T>()` in `Setup` plus a strict `.Verify` is a different smell (`dotnet.HC-1` or core `HC-6`) covered elsewhere.

---

## Framework-specific high-confidence smells (`dotnet.HC-*`)

### `dotnet.HC-1` — Moq `.Verify(...)` with a specific `Times.Exactly(N)` matching loop count

**Applies to:** `unit, integration`

**Detection:** `\.Verify\(.*Times\.Exactly\(\s*\d+\s*\)\)` where N is a small integer that also appears as a literal collection size in the Arrange section.

**Smell:** the test pins the number of calls to the collaborator to match the current implementation's loop structure. Refactoring the SUT to batch calls will break the test without changing observable behavior.

**Example (smell):**
```csharp
var items = new[] { "a", "b", "c" };
await sut.ProcessAsync(items);
repoMock.Verify(r => r.SaveAsync(It.IsAny<Item>()), Times.Exactly(3));
```

**Rewrite (intent):**
```csharp
var items = new[] { "a", "b", "c" };
await sut.ProcessAsync(items);
var saved = await repo.GetAllAsync();
saved.Select(s => s.Name).Should().BeEquivalentTo(items);
```

---

### `dotnet.HC-2` — Verifying `ILogger.Log(...)` string content as a contract

**Applies to:** `unit, integration`

**Detection:** `\.Verify\(.*ILogger|LoggerMessage|Log\(It\.Is<.*LogLevel` combined with matching on a string literal.

**Smell:** the test asserts that a log line was emitted with a particular string. Unless the log is a *published contract* (audit event, metric, structured telemetry with a schema), the log message is a development aid, not a behavior. Pinning it blocks every refactor that touches the message.

**Carve-out:** if the log call targets a structured audit-event helper (e.g. a `LoggerMessage`-generated method whose name indicates it is an audit event, or a log with a documented event-id contract), the assertion is on a published side effect — that is `POS-3`, not a smell.

**Rewrite:** use a capture helper (a `TestLogger<T>`-style fake) to assert on structured properties by key, not on rendered strings.

---

### `dotnet.HC-3` — `Assert.NotNull(x); Assert.Equal(y, x.Prop)` as the entire assertion

**Applies to:** `unit, integration`

**Detection:** an `Assert.NotNull(...)` or `.Should().NotBeNull()` followed by a single property-level assertion, with no further checks on an object whose contract is the whole shape.

**Smell:** the method's observable behavior is the full returned object; the test only pins one field. Most of the contract is unverified.

**Rewrite:** assert the whole object with `.Should().BeEquivalentTo(expected)` against a spec-derived expected value, or split into multiple tests each covering one property.

---

### `dotnet.HC-4` — Mocking an owned concrete class (`new Mock<ConcreteClass>()`)

**Applies to:** `unit` — under the integration rubric, the mock itself is already a scope leak (`I-HC-A1`); this dotnet-specific smell refines the unit-rubric finding.

**Detection:** `new Mock<([A-Z]\w*)>\(\)` where the type is a concrete class (not an interface) in the same assembly as the SUT.

**Smell:** mocking an owned concrete class means the test is simulating internal behavior instead of verifying an external boundary. The "dependency" is really a collaborator owned by the same module.

**Rewrite:** either (a) call the real collaborator — it's owned code and should be tested together — or (b) extract an interface at a genuine boundary and mock that, or (c) use a fake (a test-only implementation) instead of a mock.

---

### `dotnet.HC-5` — FluentAssertions chain with only `.Should().NotBeNull()` on a complex return

**Applies to:** `unit, integration`

**Detection:** `.Should().NotBeNull()` on a return value, with no further assertions on the object's contents, when the method returns a complex type.

**Smell:** asserts only that the method didn't return `null`, ignoring the actual contract.

**Rewrite:** assert on the returned object's properties, or on the full shape via `.BeEquivalentTo`.

---

### `dotnet.HC-6` — Single-line `[Fact]` with structural-only assertion on a nullable method

**Applies to:** `unit, integration`

**Detection:** a `[Fact]`-decorated method whose body is `var result = sut.Method(); Assert.NotNull(result);` (or `.Should().NotBeNull()`), nothing more.

**Smell:** the test is a presence check, not a behavior check. It passes for any implementation that returns non-null, including wrong ones.

**Rewrite:** either remove (if the only behavior is "doesn't crash") or add assertions on the returned value.

---

## Framework-specific low-confidence smells (`dotnet.LC-*`)

### `dotnet.LC-1` — Heavy use of `It.IsAny<T>()` across all parameters in `Setup()`

**Applies to:** `unit` — under the integration rubric, heavy `It.IsAny` usually means the dependency is mocked at all, which is already covered by `I-HC-A1` / `I-HC-B5`.

**Detection:** `Setup\(.*It\.IsAny<.*>\(\).*It\.IsAny<.*>\(\)` with 3+ `It.IsAny` in one call.

**Why low-confidence:** sometimes legitimate (testing a code path that doesn't care about specific args). Often hides intent — the author didn't know or didn't want to state what the collaborator should receive.

---

### `dotnet.LC-2` — `[Theory]` with `[InlineData]` where all cases produce the same expected value

**Applies to:** `unit, integration` — refines core `LC-8` / `I-LC-4`.

**Detection:** multiple `[InlineData(...)]` on a `[Theory]` where inspection shows every case asserts the same expected literal.

**Why low-confidence:** the parameterization isn't doing work. May indicate the author intended to cover equivalence classes but the assertion is too coarse.

---

### `dotnet.LC-3` — bUnit `.MarkupMatches(...)` against a large HTML literal

**Applies to:** `unit` — component-test specific (bUnit).

**Detection:** `\.MarkupMatches\(` followed by a multi-line string literal longer than ~5 lines.

**Why low-confidence:** bUnit's `MarkupMatches` is the right tool for component tests, but a large literal with no spec reference is a snapshot test pinning unspecified output — characterization. Short literals asserting specific user-visible text are fine.

---

### `dotnet.LC-4` — SUT constructed via reflection or `Activator.CreateInstance`

**Applies to:** `unit, integration`

**Detection:** `Activator\.CreateInstance|typeof\(.*\)\.GetConstructor` in Arrange.

**Why low-confidence:** usually means the SUT has inaccessible constructors or the test is reaching into internals.

---

### `dotnet.LC-5` — `[Trait("Category", "Slow")]` on a unit test

**Applies to:** `unit` — under the integration rubric, slow is expected and this trait is benign.

**Detection:** `\[Trait\("Category",\s*"Slow"` or `"LongRunning"` on a class/method in a project named `*.Tests` (not `*.E2E` / `*.Integration`).

**Why low-confidence:** unit tests should be fast. A slow unit test is usually an integration test mislabeled.

---

### `dotnet.LC-6` — `[Theory]` on a numeric parameter without boundary values

**Applies to:** `unit, integration` — refines core `LC-11`.

**Detection:** a `[Theory]` method with a numeric parameter (`int`, `long`, `double`, `decimal`, `float`) or collection parameter (`string`, `T[]`, `IEnumerable<T>`, `List<T>`). Collect every `[InlineData(...)]` / `[MemberData(...)]` / `[ClassData(...)]` row feeding that parameter. Flag if **none** of these boundary values appear in at least one row:

- Numeric: `0`, `1`, `-1`, `int.MaxValue`, `int.MinValue` (scale to the numeric type).
- String: `""` (empty), single-character literal, `null`.
- Collection: `new T[] {}`, `new T[] { x }`, `null`.

**Why low-confidence:** the test may be intentionally scoped to a narrow equivalence class. Always flag with a note that boundary analysis is missing; the author can dismiss if the scope is narrow by design.

**Rewrite:** add at least one boundary row, or add a separate `[Fact]` for each boundary the function is specified to handle.

---

### `dotnet.LC-7` — Positive-only test with no sibling negative test

**Applies to:** `unit, integration` — refines core `LC-12`.

**Detection:** a `[Fact]` whose name ends in `_Returns_*`, `_Succeeds`, `_Persists_*`, `_Creates_*`, `_Updates_*`, `_Completes_*`, `_Is_*` on a method that has at least one `throw new *Exception` statement, a `Result.Fail` / `Error.*` return, or `[Required]` / `[Range]` / custom validator on its input type. The method must be detected via the test's SUT construction (`var sut = new Foo(...); sut.Bar(...)`). Flag when no sibling test method on the same class targets the same method with a name matching `_Throws_*`, `_Fails_*`, `_Rejects_*`, `_Returns_Error_*`, or `_Validates_*`.

**Why low-confidence:** the test file may organize negative cases into a separate file (e.g. `OrderServiceValidationTests.cs` alongside `OrderServiceTests.cs`). Before flagging, grep the whole test project for any test whose body constructs the same SUT and targets the same method with an expected-exception pattern (`Assert.Throws<...>` / `.Should().Throw<...>()`). Only flag if zero sibling negative tests exist across the project.

**Rewrite:** add a sibling test for each distinct sad path (`POS-5` positive signal in the core rubric).

---

## Framework-specific positive signals (`dotnet.POS-*`)

### `dotnet.POS-1` — `[Theory]` with `TheoryData<...>` or `MemberData` and *varied* expected values

**Applies to:** `unit, integration`

**Why positive:** the parameterization covers equivalence classes with meaningful variation, not just repetition.

---

### `dotnet.POS-2` — `FluentAssertions` `.BeEquivalentTo(expected)` against a spec-derived expected object

**Applies to:** `unit, integration`

**Why positive:** asserts the full shape of the return value, not just a single field. When the expected object is built from a fixture or spec, the test is specification.

---

### `dotnet.POS-3` — xUnit `IClassFixture` / NUnit `[OneTimeSetUp]` used for expensive shared setup *without* mutable state

**Applies to:** `unit, integration` — especially valuable under the integration rubric, where expensive fixtures like `WebApplicationFactory<T>` are the norm and shared immutable setup is the correct way to amortize them.

**Why positive:** shared setup is unavoidable when the fixture is genuinely expensive (e.g., DI container, data protection provider). Without mutable state, it doesn't cause test interdependence.

---

### `dotnet.POS-4` — Assertions on structured log properties by key, not rendered string

**Applies to:** `unit, integration`

**Why positive:** treats the log entry as a published contract (audit event, metric) with a stable schema. Pattern typically uses a capture-helper like `TestLogger<T>` rather than `Mock<ILogger<T>>`.

---

### `dotnet.POS-5` — Capture helper (test double) instead of `Mock<ILogger<T>>`

**Applies to:** `unit, integration`

**Detection:** a `TestLogger<T>`, `CapturingLogger`, `FakeLogger` (from `Microsoft.Extensions.Logging.Testing`), or similar capture-style helper in Arrange.

**Why positive:** a capture helper is a fake (real `ILogger<T>` behavior with recording), not a mock. Assertions on the captured entries test observable behavior, not interaction.

---

### `dotnet.POS-6` — Use of `TimeProvider` (.NET 8+) with a fixed instant

**Applies to:** `unit, integration`

**Detection:** `new FakeTimeProvider(...)` or an injected `TimeProvider` with a pinned `DateTimeOffset`.

**Why positive:** the idiomatic .NET 8+ way to make time-sensitive code deterministic. Not an `HC-11` smell.

---

### `dotnet.POS-7` — Property-based test harness (FsCheck / CsCheck / Hedgehog)

**Applies to:** `unit, integration` — refines core `POS-9`.

**Detection:** any of:
- `using FsCheck;` / `using FsCheck.Xunit;` plus a `[Property]` attribute on a test method.
- `using CsCheck;` plus a `Gen.*` generator expression feeding `.Sample(...)`.
- `using Hedgehog;` plus `Property.ForAll(...)`.
- A `[Theory]` whose data source is a seeded RNG yielding values across a declared equivalence class.

**Why positive:** a property-based test expresses a domain invariant over a generated input space instead of pinning a finite set of examples. Correct implementations pass for the whole domain; characterization tests written from observed output cannot be phrased this way. Reward under both unit and integration rubrics.

---

## Framework-specific integration smells (`dotnet.I-*`)

These smells apply only under the integration rubric (step 0b selected `integration`). They refine core integration codes (`I-HC-A*`, `I-HC-B*`) with .NET-specific detection hints and rewrites.

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

## SUT surface enumeration

Consumed by [SKILL.md § SUT surface enumeration](../SKILL.md#sut-surface-enumeration) — step 2.5 of the deep-mode workflow. This section declares the .NET-specific grep patterns the audit agent uses to enumerate testable symbols in a SUT and cross-reference them against a test project.

### SUT identification

For a given test project (`tests/Foo.Tests/Foo.Tests.csproj`):

1. Parse the `<ItemGroup>` sections of the csproj and collect every `<ProjectReference Include="..." />` entry.
2. For each referenced project, resolve the absolute path relative to the test csproj.
3. Recurse: for each referenced project, parse its csproj and follow its own `<ProjectReference>` entries.
4. Stop at projects whose SDK is **not** a production-code SDK (i.e. a test SDK like `Microsoft.NET.Sdk` + `xunit`/`bunit` references). The closure is the SUT.

In this repo, for example:

- `tests/Lfm.Api.Tests` → SUT closure: `api/Lfm.Api.csproj` + `shared/Lfm.Shared.csproj`.
- `tests/Lfm.App.Core.Tests` → SUT closure: `app/Lfm.App.Core/Lfm.App.Core.csproj` + `shared/Lfm.Shared.csproj`.
- `tests/Lfm.App.Tests` → SUT closure: `app/Lfm.App.csproj` (Blazor WASM) + `app/Lfm.App.Core/Lfm.App.Core.csproj` + `shared/Lfm.Shared.csproj`.

### Grep patterns per gap class

All patterns are case-sensitive ripgrep expressions applied to `.cs` files in the SUT. Each match returns a symbol identifier plus `file:line`.

**`Gap-API` — public methods and types.** Multi-line aware. Detection patterns:

- Public classes / records / structs / interfaces: `^\s*public\s+(sealed\s+|abstract\s+|static\s+|partial\s+)*(class|record|record\s+struct|struct|interface)\s+(?P<name>[A-Z][A-Za-z0-9_]*)`.
- Public instance or static methods: `^\s*public\s+(static\s+|virtual\s+|override\s+|sealed\s+|async\s+|new\s+)*([A-Za-z0-9_<>?,\[\]\s]+)\s+(?P<name>[A-Z][A-Za-z0-9_]*)\s*\(` — then exclude matches where the captured name is a keyword, a constructor (same as the class name), or a C# operator. Ignore files under `obj/`, `bin/`, and generator-output paths.

**`Gap-Route` — HTTP and Functions routes.** Detection patterns:

- Azure Functions isolated: `\[Function\("(?P<name>[^"]+)"\)\]` — capture the function name and any adjacent `[HttpTrigger(...)]` route template.
- HTTP trigger route: `\[HttpTrigger\([^)]*,\s*Route\s*=\s*"(?P<route>[^"]+)"\)\]`.
- HTTP method + route in `HttpTrigger` args: `\[HttpTrigger\(AuthorizationLevel\.[A-Za-z]+,\s*"(?P<methods>[^"]+)"(?:,\s*Route\s*=\s*"(?P<route>[^"]+)")?`.
- ASP.NET Core minimal API: `app\.Map(Get|Post|Put|Delete|Patch)\s*\(\s*"(?P<route>[^"]+)"`.
- ASP.NET Core MVC attribute routing: `\[Route\("(?P<route>[^"]+)"\)\]` and `\[Http(Get|Post|Put|Delete|Patch)(\("(?P<route>[^"]+)"\))?\]`.

**`Gap-Migration` — database migration classes.** Detection patterns:

- Any class in `api/Migrations/` that inherits from or implements a migration base type: `:\s*(?:IAsync)?Migration\b` or `:\s*MigrationBase\b`.
- Any file whose name matches `\d{4}_[a-z0-9_]+\.cs` under `api/Migrations/` — treat the class name declared at top-of-file as the migration identifier even if the base type is missing (documented repo convention).

**`Gap-Throw` — exception throw sites.** Detection patterns:

- `throw\s+new\s+(?P<type>[A-Z][A-Za-z0-9_]*Exception)\s*\(` — capture exception type.
- Record the containing method via the nearest preceding `public|internal|private|protected` method declaration; the audit agent walks up from the match to the enclosing method name.
- Exclude re-throws (`throw;` and `throw ex;`) — those are not new sites.

**`Gap-Validate` — validation attributes on input types.** Detection patterns:

- `\[(Required|StringLength|MaxLength|MinLength|Range|RegularExpression|EmailAddress|Url|CreditCard|Phone)(\([^)]*\))?\]` on a property declaration.
- Capture the containing record / class (input type) and the property name — e.g. `CreateOrderRequest.CustomerId`.

### Cross-reference matching

For each enumerated symbol, the audit agent searches the test project tree (`tests/**/*.cs` except `obj/`, `bin/`, `TestResults/`, `StrykerOutput/`) for at least one of:

- **`Gap-API`** — the symbol name appears as an identifier in any test method name (`public void CancelOrderAsync_...`) or test body (`sut.CancelOrderAsync(...)`, `new CancelOrderAsync...`). Word-boundary match: `\bCancelOrderAsync\b`.
- **`Gap-Route`** — the route template appears as a string literal in any test body. Match the exact template after normalising case: `"orders/{id}"` or `"orders"`. Also match the Functions name from `[Function("...")]` as a string literal.
- **`Gap-Migration`** — the migration class name appears as an identifier in any test body, or the migration file name appears as a path literal.
- **`Gap-Throw`** — both the exception type (e.g. `InvalidOperationException`) *and* the containing method name appear in the same test method body. If either is missing, the throw site is a probable gap.
- **`Gap-Validate`** — the input type's property name (e.g. `CustomerId`) appears in a test body that also references the input type (e.g. `new CreateOrderRequest { CustomerId = null }`).

### Known indirect-coverage patterns (carve-outs)

These patterns suppress a false-positive `Gap-API` entry:

- A service method `Foo.BarAsync(...)` is covered indirectly when a Functions endpoint `Foo.BarFunction` that wraps it has a test, and the service type is registered in DI under the Functions project. Search DI registrations (`services.AddScoped<Foo>()` / `services.AddSingleton<Foo>()`) in the Functions project to establish wrapping; if a test exercises the wrapping endpoint, the service method is *indirectly covered* — record as "indirectly covered via `FooFunction`" and suppress the `Gap-API` entry.
- A `MigrationRunner.RunAsync` test in `tests/Lfm.Api.Tests/` that exercises the runner with seed data covers every migration transitively if the test explicitly asserts post-state for each migration class. Search for the pattern and suppress `Gap-Migration` entries for the covered classes.

### Confidence annotations

- `Gap-API`: **medium** — indirect coverage via controllers / Functions / facade methods is common in this repo.
- `Gap-Route`, `Gap-Migration`, `Gap-Validate`: **high** — these are registered by string or class identity with few indirect-coverage paths.
- `Gap-Throw`: **medium** — generic error-path tests often exercise the method without naming the exception type.

### Recommended `--mutate` follow-up

When the gap report lists a probable `Gap-API` finding on a SUT shape that Stryker.NET supports, the audit agent may suggest a targeted mutation run to confirm: `dotnet stryker --mutate "<path>.cs" --reporter cleartext` (fast — seconds).

---

## Auth matrix enumeration

Consumed by [SKILL.md § Auth matrix enumeration](../SKILL.md#auth-matrix-enumeration) — step 2.6 of the deep-mode workflow.

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

Consumed by [SKILL.md § Migration upgrade-path enumeration](../SKILL.md#migration-upgrade-path-enumeration) — step 2.7 of the deep-mode workflow.

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

---

## Carve-outs

Patterns that look like core smells but are idiomatic in .NET and must not be flagged:

- **Do not flag `HC-5`** (mock-return-then-mock-called-with) when the mock is `Mock<HttpMessageHandler>` and the verified call is `.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ...)`. This is the supported way to stub `HttpClient` behavior in .NET; `HttpMessageHandler` is a process boundary.

- **Do not flag `HC-11`** (hardcoded clock values) when the clock is injected via `TimeProvider` (including `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`) with a fixed `DateTimeOffset`. That is the idiomatic way to test time-sensitive logic in modern .NET.

- **Do not flag `LC-1`** (mocking same-layer code) when the mocked type is an interface owned by the tested module *and* the project has a documented "test via seams" convention (e.g. a `CLAUDE.md` or `README.md` stating that interfaces exist specifically for testability). Ask before flagging if ambiguous.

- **Do not flag `LC-7`** (excessive setup) when the setup is constructing an `IHost`, `WebApplicationFactory<T>`, `HostBuilder`, `TestServer`, an `IPlaywright` / `IBrowser` / `IBrowserContext` / `IPage`, an `IWebDriver`, a Testcontainers-based stack fixture, or a collection-level fixture that brings up a full backend for an E2E run. Under the new dispatch model (see [SKILL.md § 0b (Rubric selection)](../SKILL.md#0b-select-the-rubric)), these are **routing signals into the integration or E2E rubric** — tests using them should be audited under that rubric where heavy setup is expected, not the unit rubric at all. This carve-out stays in force as a **safety net for cases where the dispatch is uncertain**: if a test somehow reaches the unit rubric with one of these setups, suppress the `LC-7` finding rather than flagging a test that was misrouted.

- **Do not flag `HC-10`** (snapshot tests pinning unspecified output) when the snapshot target is a JSON response whose schema is published via an OpenAPI document in the repo, a gRPC proto, or an equivalent contract document. Reference the contract in the carve-out decision.

- **Do not flag `dotnet.HC-2`** (logger content as contract) when the log call is via a source-generated `[LoggerMessage]` method whose name is namespaced as an audit event (e.g. `LogAuditUserDeleted`) — the event *is* the contract.

---

## Mutation tool

The core skill runs mutation testing conditionally in deep mode (see [SKILL.md § Mutation testing (conditional)](../SKILL.md#mutation-testing-conditional)). It uses the subsections below to decide whether Stryker.NET is available, how to run it, and whether the SUT is a shape the tool can handle.

### 1. Tool name and link

**[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/)** — mutation testing for .NET Core and .NET Framework. Prefer it over coverage-only tools.

### 2. Install instructions

The preferred install path is a **local tool manifest** so the Stryker version is pinned in git and every contributor gets the same tool. From the repo root:

```bash
dotnet new tool-manifest        # creates .config/dotnet-tools.json (skip if it already exists)
dotnet tool install dotnet-stryker
```

Commit `.config/dotnet-tools.json`. Future contributors run `dotnet tool restore` to get the same version. Add `StrykerOutput/` to `.gitignore` — Stryker writes its reports there and they should not be committed.

If the user has `StrykerOutput/` missing from `.gitignore`, suggest adding it as part of the install step; do not add it unilaterally.

Global install (fallback, not preferred because it's unpinned):

```bash
dotnet tool install -g dotnet-stryker
```

### 3. Detection command

Check whether Stryker is installed and invokable from the audit target. The audit agent runs this before attempting a mutation run and skips the step gracefully if it exits non-zero.

```bash
# Preferred: local manifest (exit 0 if installed locally)
dotnet tool list --local 2>/dev/null | grep -q dotnet-stryker \
  || dotnet tool list --global 2>/dev/null | grep -q dotnet-stryker
```

If the repo has a `.config/dotnet-tools.json`, also run `dotnet tool restore` before the detection command — the tool may be declared but not yet restored on a fresh clone.

### 4. Run command

Run from the **test project directory**, not the solution root. Stryker auto-discovers the SUT from the test project's `<ProjectReference>` entries.

**Baseline (full project):**

```bash
cd tests/<TestProject>
dotnet stryker --reporter json --reporter cleartext --reporter html
```

- `--reporter json` produces `StrykerOutput/<timestamp>/reports/mutation-report.json` — this is what the audit agent parses to extract scores and surviving-mutant details.
- `--reporter cleartext` produces the summary table printed at the end of the run — used for a quick human-readable snapshot.
- `--reporter html` produces a browser-viewable report with surviving mutants highlighted inline in source — useful for the user after the audit.

**PR-scoped (fast):**

```bash
dotnet stryker --since main --reporter json
```

Only mutates files changed since the `main` branch. Use this when the audit target is a PR diff rather than the full test suite.

**Single-file (demo / targeted):**

```bash
dotnet stryker --mutate "**/<FileName>.cs" --reporter cleartext
```

Useful for demonstrating mutation testing on one file without waiting for a full run. Typical runtime: seconds.

**Useful flags:**

- `--mutation-level` — controls aggressiveness. Default is `Standard`; `Advanced` and `Complete` generate more mutants but take proportionally longer.
- `--concurrency <N>` — override default CPU parallelism. Default is all cores.
- `-b|--break-at <0-100>` — return non-zero exit code if score drops below threshold. Useful for CI gating, not for audits.
- `--diag` — enable diagnostic logging when troubleshooting a failed run.

### 5. Known SUT limitations

Stryker.NET cannot mutate every .NET SUT shape. Before attempting a run, the audit agent should check for each of these patterns and skip with the documented workaround if any match.

#### Blazor WebAssembly (`Microsoft.NET.Sdk.BlazorWebAssembly` SDK)

- **How to detect:** any project in the test project's transitive `<ProjectReference>` closure uses `<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">` or references `Microsoft.AspNetCore.Components.WebAssembly`. The direct test csproj can be a plain `Microsoft.NET.Sdk` library — Stryker still compiles the full closure during its recompile step and hits the same Razor-generator error. Walk the chain: start at the test project's csproj, read its `<ProjectReference>` entries, and recurse into each referenced csproj. Stop at the first Blazor WASM SDK match.
- **Root cause:** Blazor WASM's `Program.cs` references types like `App` that are generated at build time by the Razor source generator from `.razor` files. Stryker runs its own Roslyn compilation step for each mutation batch and does **not** invoke source generators during that step. The generated types are therefore unresolvable during Stryker's mutated-recompile, producing `CS0246: The type or namespace name 'App' could not be found` and `CS8805: Program using top-level statements must be an executable` errors. There is no Stryker config option that excludes files from the internal recompilation step — the `mutate` option (with or without `!` exclusion patterns) only controls which files receive mutants, not which files get compiled. Verified against the official docs at https://stryker-mutator.io/docs/stryker-net/configuration/ and https://stryker-mutator.io/docs/stryker-net/ignore-mutations/ (2026-04).
- **Workaround:** extract pure-C# logic (services, HTTP clients, state managers, i18n, helpers) from the Blazor WASM project into a separate class library (e.g. `<AppName>.Core`). Reference that library from the Blazor project. Create a dedicated test project for the library that references **only** the extract (no transitive path to the Blazor WASM SDK). Run Stryker against that test project — it has no Razor dependencies and mutates cleanly. Blazor component tests (e.g. bUnit) stay in the Blazor test project and continue to rely on static audit for quality signal. This is also the approach the Stryker.NET team recommends for projects using source generators of any kind. Tip: set `<RootNamespace>` on the extract csproj to the Blazor project's original root namespace so the moved files keep their declared namespaces and consumer code (`.razor`, `Program.cs`, existing tests) needs no edits.
- **Audit output when detected:** before skipping, check whether the workaround is already in place. The "already-extracted" pattern: (a) the Blazor WASM project `<ProjectReference>`s a class library that uses plain `<Project Sdk="Microsoft.NET.Sdk">` (not the WebAssembly SDK), AND (b) some test project's transitive `<ProjectReference>` closure reaches that library WITHOUT reaching any Blazor WASM SDK project. When both conditions hold, the refactor is already done — use that test project as the Stryker target.
  - **Extract already applied:** run Stryker against the extract's test project as the mutation target. Skip the Blazor-transitive test project(s) with state C (citing this subsection) and **do not** emit the `P3` refactor recommendation — the refactor is already done. Note in the Mutation testing subsection that the extract was detected and used as the target.
  - **No extract yet:** skip the Blazor-transitive test project(s) with state C, cite this subsection, and emit the extract-to-library workaround as a `P3` item in the remediation worklist. Continue the mutation run against any other non-Blazor projects in scope (e.g. a separate backend project).

#### Other source-generator-heavy projects

Projects that rely heavily on source generators (e.g. `[LoggerMessage]`-only codebases, Mapperly, Refit, MediatR source generator) may hit similar "generated type not found" errors during Stryker's mutated-recompile. The audit agent should:

1. Attempt the run.
2. If it fails with `CS0246` on a type that grep suggests is generator-produced (check for `[<GeneratorAttributeName>]` in the SUT), treat as a known limitation, report state C with the specific generator named as the root cause, and recommend a selective `--mutate` exclude of the files that reference the generator output (this may or may not help — document uncertainty honestly).

#### .NET Framework projects without `msbuild-path`

Stryker requires `--msbuild-path` on .NET Framework (classic) projects. If the SUT is .NET Framework and the detection command succeeds but the run fails with MSBuild errors, instruct the user to pass `--msbuild-path` pointing at their Visual Studio or Build Tools MSBuild installation.

### 6. Output parser notes

- **Report location:** `<test-project>/StrykerOutput/<timestamp>/reports/mutation-report.json`. The timestamp is `YYYY-MM-DD.HH-MM-SS`; pick the newest directory if multiple exist.
- **Top-level score:** the last line of the cleartext report prints `The final mutation score is N.NN %`. The JSON report stores per-file data; the overall score is computed as `(killed + timeout) / (killed + survived + timeout + no_coverage)`. Ignored and compile-error mutants are excluded from the denominator.
- **Per-file extraction:** iterate `.files` in the JSON; for each file, group `.mutants` by `.status`. Meaningful statuses are `Killed`, `Survived`, `NoCoverage`, `Timeout`, `Ignored`, `CompileError`.
- **Surviving-mutant details:** for each surviving mutant, extract `.location.start.line`, `.mutatorName`, and `.replacement` (or `.description`) to show what was changed. This is the raw material for the "audit-vs-mutation disagreement" reconciliation in step 5 of deep-mode output.
- **Files entirely without tests:** filter for files whose mutant list has zero `Killed` + zero `Survived` + zero `Timeout` entries. These are the "no test touches this file" findings the static audit cannot see.

### When to run it

**Always run in deep mode when the detection command succeeds**, regardless of which smells the static audit found. Mutation testing's highest-value output is the audit-vs-mutation disagreement: files rated `strong` by static audit that have surviving mutants. Those disagreements only surface if you run the tool unconditionally on a successful-audit suite.

If the suite has many `HC-1` / `HC-3` / `HC-5` / `HC-6` / `dotnet.HC-5` / `dotnet.HC-6` findings, the mutation run is especially valuable — those smells all indicate tests that execute code without verifying it, which mutation testing surfaces mechanically — but this is not a gating criterion.
