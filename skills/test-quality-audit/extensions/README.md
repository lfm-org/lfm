# Extensions

Per-stack smell packs for the `test-quality-audit` skill. The core rubric in [../SKILL.md](../SKILL.md) is deliberately framework-neutral; extensions add the framework-specific detail that would otherwise bloat the core or cause false positives when applied to the wrong stack.

## Purpose

Extensions **augment** the core rubric. They can:

- Add framework-specific smells (high-confidence and low-confidence).
- Add framework-specific positive signals to reward.
- **Carve out** core smells that would produce false positives on idiomatic framework patterns.
- Name the mutation-testing tool to recommend for that stack.

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
2. **Framework-specific high-confidence smells** (`<ext>.HC-N`) — each entry has a short description, a detection hint (grep pattern, AST shape, or semantic signal), an example of the smell, and an intent-preserving rewrite.
3. **Framework-specific low-confidence smells** (`<ext>.LC-N`) — same shape; flagged as warnings that need context.
4. **Framework-specific positive signals** (`<ext>.POS-N`) — patterns to reward explicitly.
5. **Carve-outs** — explicit list of core smells suppressed for idiomatic patterns in this stack. Each carve-out references the core code it suppresses and describes the exact pattern.
6. **Mutation tool recommendation** — the mutation-testing tool appropriate for this stack, with a link.

## Naming codes

Extension codes are namespaced as `<ext>.HC-N`, `<ext>.LC-N`, `<ext>.POS-N` where `<ext>` is the extension filename without `.md`. Example: `dotnet.HC-1` is the first high-confidence .NET smell.

Core codes stay unnamespaced (`HC-1`, `LC-3`, `POS-2`).

## When to add a new extension

Add an extension when the audit agent's static analysis on a stack produces obvious false positives or negatives because the core rubric lacks framework context. Signs it's time:

- A recurring pattern in that stack trips a core smell but is actually idiomatic.
- A common anti-pattern in that stack has no core smell to match it.
- The audit keeps recommending a generic mutation tool when a stack-specific one would be more useful.

## Keep extensions small

A few hundred lines maximum. If an extension grows beyond that, split by test framework within the stack (`dotnet-xunit.md`, `dotnet-bunit.md`) rather than nesting deeply in one file.

Concise extensions load cheaply — the audit agent may load several at once for polyglot repos.
