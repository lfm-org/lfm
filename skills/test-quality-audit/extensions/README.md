# Extensions

Per-stack smell packs for the `test-quality-audit` skill. The core rubric in [../SKILL.md](../SKILL.md) is deliberately framework-neutral; extensions add the framework-specific detail that would otherwise bloat the core or cause false positives when applied to the wrong stack.

## Purpose

Extensions **augment** the core rubric. They can:

- Add framework-specific smells (high-confidence and low-confidence) for both the unit and integration rubrics.
- Add framework-specific positive signals to reward.
- **Carve out** core smells that would produce false positives on idiomatic framework patterns.
- Declare **test type detection signals** that route a test to the integration rubric instead of the unit rubric. Consumed by [../SKILL.md § 0b (Rubric selection)](../SKILL.md#0b-select-the-rubric).
- Declare the mutation-testing tool for that stack, with a detection command, run command, install instructions, and a known-unsupported SUT list. The core skill uses this declaration to run mutation testing conditionally in deep mode (see [../SKILL.md § Mutation testing (conditional)](../SKILL.md#mutation-testing-conditional)).

Extensions **never override** core rules. A carve-out suppresses a specific core smell only for the exact pattern it describes. When in doubt, prefer the core rule.

## File layout

One `.md` file per stack, named after the language or ecosystem — not after a single test framework — because one stack usually supports multiple frameworks:

- `dotnet.md` — covers xUnit, NUnit, MSTest, bUnit, and the usual mocking/assertion libraries.
- `javascript.md` — would cover Jest, Vitest, Mocha, and the Testing Library family.
- `python.md` — would cover pytest and unittest.
- `java.md` — would cover JUnit 5, TestNG, Mockito.
- `go.md` — would cover the standard `testing` package and testify.

## Required sections

Every extension file must include these sections, in this order:

1. **Detection signals** — how the audit agent knows this extension applies. Glob patterns, manifest files, package references, file extensions.
2. **Test type detection signals** — patterns that route a test to the integration rubric instead of the unit rubric (step 0b of the audit workflow). Split into: **integration rubric signals** (project-level, using-directive-level, construction-level, real-infrastructure-helper-level, and emulator-endpoint-level patterns that route the test to integration) and **unit rubric signals** (the default — typically "SUT constructed directly with mocked dependencies"). Extensions without this section default all tests to the unit rubric — explicit and backwards compatible.
3. **Framework-specific high-confidence smells** (`<ext>.HC-N`, `<ext>.I-HC-A-N`, `<ext>.I-HC-B-N`) — each entry has a short description, an `Applies to:` field, a detection hint (grep pattern, AST shape, or semantic signal), an example of the smell, and an intent-preserving rewrite. Entries with core-unit codes `<ext>.HC-N` apply to the unit rubric (or both rubrics, see *Applies-to field* below); entries with integration codes `<ext>.I-HC-A-N` / `<ext>.I-HC-B-N` apply only under the integration rubric and refine the core `I-HC-A*` / `I-HC-B*` codes with framework-specific detection hints.
4. **Framework-specific low-confidence smells** (`<ext>.LC-N`, `<ext>.I-LC-N`) — same shape; flagged as warnings that need context.
5. **Framework-specific positive signals** (`<ext>.POS-N`, `<ext>.I-POS-N`) — patterns to reward explicitly.
6. **Framework-specific integration smells section** (optional grouping) — extensions may group their integration-only smells (`<ext>.I-HC-A-N`, `<ext>.I-HC-B-N`, `<ext>.I-LC-N`, `<ext>.I-POS-N`) in a dedicated section after the unit-rubric smells for readability. Required content is the same as sections 3–5; only the grouping is optional.
7. **Carve-outs** — explicit list of core smells suppressed for idiomatic patterns in this stack. Each carve-out references the core code it suppresses and describes the exact pattern.
8. **Mutation tool** — the mutation-testing tool appropriate for this stack. This section is consumed by the core skill's deep-mode workflow; it must include these subsections in this order:
   1. **Tool name and link.**
   2. **Install instructions** — the exact commands a user should run to install the tool. These are printed verbatim in the audit output when the tool is not installed, so that the user can enable mutation testing for a future audit.
   3. **Detection command** — a cheap, side-effect-free shell command the audit agent runs to check whether the tool is installed. Must exit non-zero when not installed. Example: `dotnet tool list | grep -q <tool-name>`.
   4. **Run command** — the exact command the audit agent runs when the tool is available, including the recommended reporters for machine-readable output.
   5. **Known SUT limitations** — a bulleted list of SUT shapes the tool cannot handle, each with: (a) how to detect the shape, (b) the root cause of the incompatibility, and (c) the recommended workaround. The audit agent uses this list to decide whether to skip the mutation run with a documented reason rather than attempting a doomed run. Example: "Blazor WebAssembly SUTs — the Razor source generator does not run inside Stryker's internal Roslyn compiler, so `App` and similar generated types are unresolvable during mutation recompilation. Workaround: extract pure-C# logic to a separate class library and mutate that library instead."
   6. **Output parser notes** — where the tool writes its JSON/HTML report and which fields the audit agent should extract (overall score, per-file killed/survived/no-coverage, surviving-mutant locations).

## Naming codes

Extension codes are namespaced as `<ext>.HC-N`, `<ext>.LC-N`, `<ext>.POS-N` for unit-rubric smells, and `<ext>.I-HC-A-N`, `<ext>.I-HC-B-N`, `<ext>.I-LC-N`, `<ext>.I-POS-N` for integration-rubric smells, where `<ext>` is the extension filename without `.md`. Example: `dotnet.HC-1` is the first high-confidence .NET unit-rubric smell; `dotnet.I-HC-A1` is the first high-confidence .NET integration-rubric smell in sub-lane A.

Core codes stay unnamespaced (`HC-1`, `LC-3`, `POS-2`, `I-HC-A1`, `I-HC-B5`, `I-LC-4`, `I-POS-7`).

### Applies-to field

Every extension smell entry must declare an `Applies to:` field on its own line directly under the smell heading, before the detection / smell / example / rewrite blocks. The field takes one of three values:

- `Applies to: unit` — applies only when step 0b selects the unit rubric.
- `Applies to: integration` — applies only when step 0b selects the integration rubric. Typically used by `<ext>.I-*` codes.
- `Applies to: unit, integration` — applies under both rubrics. Typical for framework-specific smells that are rubric-neutral (e.g. logger-content-as-contract, presence-check assertions).

Smells that omit the `Applies to:` field default to `unit` for backwards compatibility. Existing pre-integration-rubric extensions therefore continue to work unchanged.

## When to add a new extension

Add an extension when the audit agent's static analysis on a stack produces obvious false positives or negatives because the core rubric lacks framework context. Signs it's time:

- A recurring pattern in that stack trips a core smell but is actually idiomatic.
- A common anti-pattern in that stack has no core smell to match it.
- The audit keeps recommending a generic mutation tool when a stack-specific one would be more useful.

## Keep extensions small

A few hundred lines maximum. If an extension grows beyond that, split by test framework within the stack (`dotnet-xunit.md`, `dotnet-bunit.md`) rather than nesting deeply in one file.

Concise extensions load cheaply — the audit agent may load several at once for polyglot repos.
