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
