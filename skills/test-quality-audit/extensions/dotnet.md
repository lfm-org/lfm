# Extension: .NET

Covers .NET unit-test stacks: xUnit, NUnit, MSTest, bUnit (Blazor component testing), and the commonly-used mocking and assertion libraries (Moq, NSubstitute, FakeItEasy, FluentAssertions).

## Detection signals

Load this extension when the audit target contains any of:

- `*.csproj` or `*.sln` files.
- A `.cs` file with `using Xunit;` / `using NUnit.Framework;` / `using Microsoft.VisualStudio.TestTools.UnitTesting;` / `using Bunit;`.
- A `.csproj` with `<PackageReference Include="xunit"` / `"nunit"` / `"MSTest.TestAdapter"` / `"bunit"` / `"Moq"` / `"NSubstitute"` / `"FakeItEasy"` / `"FluentAssertions"`.
- A `global.json` or `dotnet-tools.json` in the target tree.

Detection glob shortcuts: `**/*.csproj`, `**/*Tests.cs`, `**/*Tests/*.cs`, `**/Tests/**/*.cs`.

---

## Framework-specific high-confidence smells (`dotnet.HC-*`)

### `dotnet.HC-1` — Moq `.Verify(...)` with a specific `Times.Exactly(N)` matching loop count

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

**Detection:** `\.Verify\(.*ILogger|LoggerMessage|Log\(It\.Is<.*LogLevel` combined with matching on a string literal.

**Smell:** the test asserts that a log line was emitted with a particular string. Unless the log is a *published contract* (audit event, metric, structured telemetry with a schema), the log message is a development aid, not a behavior. Pinning it blocks every refactor that touches the message.

**Carve-out:** if the log call targets a structured audit-event helper (e.g. a `LoggerMessage`-generated method whose name indicates it is an audit event, or a log with a documented event-id contract), the assertion is on a published side effect — that is `POS-3`, not a smell.

**Rewrite:** use a capture helper (a `TestLogger<T>`-style fake) to assert on structured properties by key, not on rendered strings.

---

### `dotnet.HC-3` — `Assert.NotNull(x); Assert.Equal(y, x.Prop)` as the entire assertion

**Detection:** an `Assert.NotNull(...)` or `.Should().NotBeNull()` followed by a single property-level assertion, with no further checks on an object whose contract is the whole shape.

**Smell:** the method's observable behavior is the full returned object; the test only pins one field. Most of the contract is unverified.

**Rewrite:** assert the whole object with `.Should().BeEquivalentTo(expected)` against a spec-derived expected value, or split into multiple tests each covering one property.

---

### `dotnet.HC-4` — Mocking an owned concrete class (`new Mock<ConcreteClass>()`)

**Detection:** `new Mock<([A-Z]\w*)>\(\)` where the type is a concrete class (not an interface) in the same assembly as the SUT.

**Smell:** mocking an owned concrete class means the test is simulating internal behavior instead of verifying an external boundary. The "dependency" is really a collaborator owned by the same module.

**Rewrite:** either (a) call the real collaborator — it's owned code and should be tested together — or (b) extract an interface at a genuine boundary and mock that, or (c) use a fake (a test-only implementation) instead of a mock.

---

### `dotnet.HC-5` — FluentAssertions chain with only `.Should().NotBeNull()` on a complex return

**Detection:** `.Should().NotBeNull()` on a return value, with no further assertions on the object's contents, when the method returns a complex type.

**Smell:** asserts only that the method didn't return `null`, ignoring the actual contract.

**Rewrite:** assert on the returned object's properties, or on the full shape via `.BeEquivalentTo`.

---

### `dotnet.HC-6` — Single-line `[Fact]` with structural-only assertion on a nullable method

**Detection:** a `[Fact]`-decorated method whose body is `var result = sut.Method(); Assert.NotNull(result);` (or `.Should().NotBeNull()`), nothing more.

**Smell:** the test is a presence check, not a behavior check. It passes for any implementation that returns non-null, including wrong ones.

**Rewrite:** either remove (if the only behavior is "doesn't crash") or add assertions on the returned value.

---

## Framework-specific low-confidence smells (`dotnet.LC-*`)

### `dotnet.LC-1` — Heavy use of `It.IsAny<T>()` across all parameters in `Setup()`

**Detection:** `Setup\(.*It\.IsAny<.*>\(\).*It\.IsAny<.*>\(\)` with 3+ `It.IsAny` in one call.

**Why low-confidence:** sometimes legitimate (testing a code path that doesn't care about specific args). Often hides intent — the author didn't know or didn't want to state what the collaborator should receive.

---

### `dotnet.LC-2` — `[Theory]` with `[InlineData]` where all cases produce the same expected value

**Detection:** multiple `[InlineData(...)]` on a `[Theory]` where inspection shows every case asserts the same expected literal.

**Why low-confidence:** the parameterization isn't doing work. May indicate the author intended to cover equivalence classes but the assertion is too coarse.

---

### `dotnet.LC-3` — bUnit `.MarkupMatches(...)` against a large HTML literal

**Detection:** `\.MarkupMatches\(` followed by a multi-line string literal longer than ~5 lines.

**Why low-confidence:** bUnit's `MarkupMatches` is the right tool for component tests, but a large literal with no spec reference is a snapshot test pinning unspecified output — characterization. Short literals asserting specific user-visible text are fine.

---

### `dotnet.LC-4` — SUT constructed via reflection or `Activator.CreateInstance`

**Detection:** `Activator\.CreateInstance|typeof\(.*\)\.GetConstructor` in Arrange.

**Why low-confidence:** usually means the SUT has inaccessible constructors or the test is reaching into internals.

---

### `dotnet.LC-5` — `[Trait("Category", "Slow")]` on a unit test

**Detection:** `\[Trait\("Category",\s*"Slow"` or `"LongRunning"` on a class/method in a project named `*.Tests` (not `*.E2E` / `*.Integration`).

**Why low-confidence:** unit tests should be fast. A slow unit test is usually an integration test mislabeled.

---

## Framework-specific positive signals (`dotnet.POS-*`)

### `dotnet.POS-1` — `[Theory]` with `TheoryData<...>` or `MemberData` and *varied* expected values

**Why positive:** the parameterization covers equivalence classes with meaningful variation, not just repetition.

---

### `dotnet.POS-2` — `FluentAssertions` `.BeEquivalentTo(expected)` against a spec-derived expected object

**Why positive:** asserts the full shape of the return value, not just a single field. When the expected object is built from a fixture or spec, the test is specification.

---

### `dotnet.POS-3` — xUnit `IClassFixture` / NUnit `[OneTimeSetUp]` used for expensive shared setup *without* mutable state

**Why positive:** shared setup is unavoidable when the fixture is genuinely expensive (e.g., DI container, data protection provider). Without mutable state, it doesn't cause test interdependence.

---

### `dotnet.POS-4` — Assertions on structured log properties by key, not rendered string

**Why positive:** treats the log entry as a published contract (audit event, metric) with a stable schema. Pattern typically uses a capture-helper like `TestLogger<T>` rather than `Mock<ILogger<T>>`.

---

### `dotnet.POS-5` — Capture helper (test double) instead of `Mock<ILogger<T>>`

**Detection:** a `TestLogger<T>`, `CapturingLogger`, `FakeLogger` (from `Microsoft.Extensions.Logging.Testing`), or similar capture-style helper in Arrange.

**Why positive:** a capture helper is a fake (real `ILogger<T>` behavior with recording), not a mock. Assertions on the captured entries test observable behavior, not interaction.

---

### `dotnet.POS-6` — Use of `TimeProvider` (.NET 8+) with a fixed instant

**Detection:** `new FakeTimeProvider(...)` or an injected `TimeProvider` with a pinned `DateTimeOffset`.

**Why positive:** the idiomatic .NET 8+ way to make time-sensitive code deterministic. Not an `HC-11` smell.

---

## Carve-outs

Patterns that look like core smells but are idiomatic in .NET and must not be flagged:

- **Do not flag `HC-5`** (mock-return-then-mock-called-with) when the mock is `Mock<HttpMessageHandler>` and the verified call is `.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ...)`. This is the supported way to stub `HttpClient` behavior in .NET; `HttpMessageHandler` is a process boundary.

- **Do not flag `HC-11`** (hardcoded clock values) when the clock is injected via `TimeProvider` (including `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`) with a fixed `DateTimeOffset`. That is the idiomatic way to test time-sensitive logic in modern .NET.

- **Do not flag `LC-1`** (mocking same-layer code) when the mocked type is an interface owned by the tested module *and* the project has a documented "test via seams" convention (e.g. a `CLAUDE.md` or `README.md` stating that interfaces exist specifically for testability). Ask before flagging if ambiguous.

- **Do not flag `LC-7`** (excessive setup) when the setup is constructing an `IHost`, `WebApplicationFactory<T>`, `HostBuilder`, or `TestServer`. Integration-style unit tests need that setup by construction; a long Arrange block there is expected.

- **Do not flag `HC-10`** (snapshot tests pinning unspecified output) when the snapshot target is a JSON response whose schema is published via an OpenAPI document in the repo, a gRPC proto, or an equivalent contract document. Reference the contract in the carve-out decision.

- **Do not flag `dotnet.HC-2`** (logger content as contract) when the log call is via a source-generated `[LoggerMessage]` method whose name is namespaced as an audit event (e.g. `LogAuditUserDeleted`) — the event *is* the contract.

---

## Mutation tool recommendation

**Primary:** [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/).

Install per-project:

```
dotnet tool install -g dotnet-stryker
```

Run from the test project directory:

```
dotnet stryker
```

Useful flags:

- `--since main` — scope mutations to files changed since the `main` branch (fast PR-scoped runs).
- `--mutation-level` — control aggressiveness; `Standard` is the default.
- `--reporter html` — produce a visual report of surviving mutants.

**When to recommend it:** when the deep-mode audit finds the suite looks high-coverage but shallow — specifically, when per-test findings include many `HC-1` / `HC-3` / `HC-5` / `HC-6` / `dotnet.HC-5` / `dotnet.HC-6` results. These smells all indicate tests that execute code without verifying it, which is exactly what mutation testing surfaces mechanically.
