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

One set of `.md` files per stack, named after the language or ecosystem — not after a single test framework — because one stack usually supports multiple frameworks. A stack extension uses one of two shapes:

### Single-file

Everything lives in `<stack>.md`. Use when the stack's smells and procedures fit under the per-file size target. Example candidates:

- `javascript.md` — would cover Jest, Vitest, Mocha, and the Testing Library family.
- `python.md` — would cover pytest and unittest.
- `java.md` — would cover JUnit 5, TestNG, Mockito.
- `go.md` — would cover the standard `testing` package and testify.

### Core + rubric addons

One `<stack>-core.md` file shared across all rubrics plus up to three `<stack>-unit.md` / `<stack>-integration.md` / `<stack>-e2e.md` files carrying only the content that is exclusive to one rubric. Use when a stack has enough rubric-neutral content (smells marked `Applies to: unit, integration`, procedures that apply under multiple rubrics) that a strict by-rubric split would force duplicating content across files.

Currently the .NET extension uses this shape:

- `dotnet-core.md` — detection signals, test-type dispatch, test-double taxonomy, rubric-neutral smells (`dotnet.HC-*`, `dotnet.LC-*`, `dotnet.POS-*`), carve-outs, SUT surface enumeration, determinism verification, and the Stryker mutation tool declaration. Always loaded when .NET is detected.
- `dotnet-unit.md` — `Applies to: unit` smells only (`dotnet.HC-4`, `dotnet.LC-1`, `dotnet.LC-3`, `dotnet.LC-5`). Loaded when step 0b selects the unit rubric.
- `dotnet-integration.md` — `dotnet.I-*` smells, auth matrix enumeration, migration upgrade-path enumeration. Loaded when step 0b selects the integration rubric.
- `dotnet-e2e.md` — E2E sub-lane refinements and future `dotnet.E-*` smells (stub today). Loaded when step 0b selects the E2E rubric.

**Loading rule for core + addon extensions:** always load the `-core.md` file (it owns detection and test-type dispatch). After step 0b selects the rubric(s), load the addon that matches each selected rubric. Load multiple addons for mixed-rubric audit targets.

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

A few hundred lines per file is the target. If an extension grows beyond that, split the file. Two split shapes are supported:

- **By rubric (preferred when content is rubric-partitioned).** Extract a `<stack>-core.md` for rubric-neutral content and create `<stack>-unit.md` / `<stack>-integration.md` / `<stack>-e2e.md` addons for rubric-exclusive content. See the `dotnet` extension for the current reference implementation. The skill loads only what the selected rubric needs.
- **By test framework (preferred when content is framework-partitioned).** For stacks whose smells cluster around specific frameworks with little shared content, split into `<stack>-<framework>.md` files — e.g. `dotnet-xunit.md`, `dotnet-bunit.md`. Each file stays focused on one framework's idioms.

Pick the split that minimises duplication. If most of the stack's smells declare `Applies to: unit, integration` (rubric-neutral), the by-rubric split with a core file avoids duplicating those smells across addon files. If most of the stack's smells are framework-specific with little rubric overlap, the by-framework split is cleaner.

Concise extensions load cheaply — the audit agent may load several at once for polyglot repos.
