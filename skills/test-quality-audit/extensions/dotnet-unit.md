# Extension: .NET — unit-rubric addon

Addon to [dotnet-core.md](dotnet-core.md) loaded **only when step 0b selects the unit rubric** (or `component`, which is unit-equivalent). Carries framework-specific smells that apply exclusively under the unit rubric — typically because the smell they target is already covered by a core integration-rubric smell (`I-HC-A1` / `I-HC-B5` etc.) and would double-fire if listed as rubric-neutral.

**Prerequisite:** `dotnet-core.md` must already be loaded. Everything in this file refines or depends on content defined there.

---

## Framework-specific high-confidence smells (unit-only)

### `dotnet.HC-4` — Mocking an owned concrete class (`new Mock<ConcreteClass>()`)

**Applies to:** `unit` — under the integration rubric, the mock itself is already a scope leak (`I-HC-A1`); this dotnet-specific smell refines the unit-rubric finding.

**Detection:** `new Mock<([A-Z]\w*)>\(\)` where the type is a concrete class (not an interface) in the same assembly as the SUT.

**Smell:** mocking an owned concrete class means the test is simulating internal behavior instead of verifying an external boundary. The "dependency" is really a collaborator owned by the same module.

**Rewrite:** either (a) call the real collaborator — it's owned code and should be tested together — or (b) extract an interface at a genuine boundary and mock that, or (c) use a fake (a test-only implementation) instead of a mock.

---

## Framework-specific low-confidence smells (unit-only)

### `dotnet.LC-1` — Heavy use of `It.IsAny<T>()` across all parameters in `Setup()`

**Applies to:** `unit` — under the integration rubric, heavy `It.IsAny` usually means the dependency is mocked at all, which is already covered by `I-HC-A1` / `I-HC-B5`.

**Detection:** `Setup\(.*It\.IsAny<.*>\(\).*It\.IsAny<.*>\(\)` with 3+ `It.IsAny` in one call.

**Why low-confidence:** sometimes legitimate (testing a code path that doesn't care about specific args). Often hides intent — the author didn't know or didn't want to state what the collaborator should receive.

---

### `dotnet.LC-3` — bUnit `.MarkupMatches(...)` against a large HTML literal

**Applies to:** `unit` — component-test specific (bUnit).

**Detection:** `\.MarkupMatches\(` followed by a multi-line string literal longer than ~5 lines.

**Why low-confidence:** bUnit's `MarkupMatches` is the right tool for component tests, but a large literal with no spec reference is a snapshot test pinning unspecified output — characterization. Short literals asserting specific user-visible text are fine.

---

### `dotnet.LC-5` — `[Trait("Category", "Slow")]` on a unit test

**Applies to:** `unit` — under the integration rubric, slow is expected and this trait is benign.

**Detection:** `\[Trait\("Category",\s*"Slow"` or `"LongRunning"` on a class/method in a project named `*.Tests` (not `*.E2E` / `*.Integration`).

**Why low-confidence:** unit tests should be fast. A slow unit test is usually an integration test mislabeled.
