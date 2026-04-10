---
name: test-quality-audit
description: Use when auditing unit test quality — distinguishing specification tests from characterization tests, surfacing coupling smells, and producing per-test findings with severity and recommended actions. Supports quick single-file / PR-diff audits and deep suite-wide audits, with pluggable per-stack extensions for framework-specific smells.
---

# Test Quality Audit

## Overview

Audit unit tests for quality, with the central question for every test:

> Could this test have been written from a stated requirement alone, without having seen the implementation?

If no, it is a **characterization test** — it locks in current behavior, including any bugs present at the time of writing. A characterization test may still be valuable as legacy scaffolding, but it must be labeled as such. If it is unlabeled and sits alongside specification tests, it is a quality smell.

**Read [../../docs/quality-reference/unit-testing.md](../../docs/quality-reference/unit-testing.md) before auditing.** That document is the canonical rubric: principles, full rationale, and the complete smell list. This skill is the *workflow* for applying the rubric.

**Read `references/smell-catalog.md`** for the compact code list used in reports (`HC-*`, `LC-*`, `POS-*`). Audit findings cite smells by code, not by restating them.

## Audit Mode

This skill supports two modes. Choose based on the request:

### Quick mode

Use for: single file, single test, or a PR diff. Fastest feedback loop.

- Audit only the specified target.
- Per-test findings only, no file rollup, no suite-level assessment.
- Good for pre-merge review and ad-hoc "is this test any good" questions.

Trigger phrases: "audit this test", "audit these tests", "check this PR's tests", "quick test audit", "is this test good".

### Deep mode

Use for: a full test suite, a module's worth of tests, or a pre-refactor quality check.

- Enumerate all tests in scope.
- Per-test findings + per-file rollup + suite-level verdict + prioritized remediation worklist.
- Recommend a mutation-testing run if the suite looks high-coverage but shallow.

Trigger phrases: "full test audit", "audit the test suite", "deep test audit", "review all our tests", "find characterization tests".

**Default:** If the request is ambiguous, ask the user which mode they want.

---

## Extensions

The core rubric is framework-neutral. **Extensions** are per-stack smell packs loaded on demand that add framework-specific smells, positive signals, and carve-outs (explicit "do not flag" rules for patterns that look smelly in the core rubric but are idiomatic in a given framework).

Extensions live in `extensions/*.md`. Read `extensions/README.md` for the full convention. Current extensions:

- [extensions/dotnet.md](extensions/dotnet.md) — .NET / xUnit / NUnit / MSTest / bUnit / Moq / NSubstitute / FluentAssertions

### Detection phase (step 0 of every audit)

Before applying the rubric, detect which stacks are present in the audit target. Load every matching extension:

| Signal | Extension to load |
|---|---|
| `*.csproj` or `*.sln` in the target; `xunit`, `nunit`, `mstest`, `Moq`, `NSubstitute`, `bunit`, `FluentAssertions` package refs | `extensions/dotnet.md` |
| `package.json` with `jest`, `vitest`, `mocha`, `@testing-library/*` in devDependencies | *(future)* `extensions/javascript.md` |
| `pyproject.toml` or `setup.py` with `pytest` or `unittest` | *(future)* `extensions/python.md` |

Detection rules:

- **Multiple stacks** — load all matching extensions. Note to the user which were loaded.
- **No matching extension** — proceed with the core rubric only. Note the missing extension as a limitation in the output; if the stack is common, recommend that a new extension be written.
- **Framework-specific grep hints** live in the extension, not here.

### Precedence

Extensions **never override** core rules. They can:

- **Add** new smells (namespaced as `<ext>.HC-N`, `<ext>.LC-N`, `<ext>.POS-N`).
- **Carve out** core smells that would produce false positives in idiomatic framework patterns. A carve-out is an explicit `do not flag HC-X when <pattern>`.

If a carve-out and a core smell conflict, the carve-out wins *only* for the exact pattern described. When in doubt, prefer the core rule.

---

## Quick Mode Workflow

0. **Detect stack and load extensions.** Glob for project manifests (`*.csproj`, `package.json`, `pyproject.toml`, etc.) within the audit target. Read matching extension files. Announce which extensions loaded.
1. **Identify the target.** A file, a glob, a set of changed files from a PR diff, or a single test within a file.
2. **Read supporting infrastructure first.** Before reading individual tests, Read any test base classes, fixtures, or custom helpers referenced by the target tests. This prevents flagging idiomatic uses of a helper as a smell.
3. **For each test case:**
   - Read the test body.
   - Apply the per-test rubric (see below). Apply core smells first, then extension smells, then extension carve-outs.
   - Determine the verdict, severity, and recommended action.
4. **Emit findings** in the per-test shape (see Output format below). Do not restate smell descriptions — cite codes and let the reader look them up.

## Deep Mode Workflow

0. **Detect stack and load extensions.** Same as quick mode. Record which extensions loaded so the final report can disclose them.
1. **Establish scope.** Confirm which test projects or directories, and which test type (unit, component, integration). If the user said "audit the tests" without specifying, ask.
2. **Enumerate test files.** Use Glob with the extension's file patterns. For each file:
   - Skim for infrastructure (fixtures, test bases, custom helpers) before reading individual tests.
   - Apply the per-test rubric (core + loaded extensions) to each test case.
3. **Roll up per-file:**
   - Total tests.
   - Verdict counts: specification / characterization / ambiguous.
   - Top smells by frequency.
   - File-level quality: `strong` / `adequate` / `weak` / `not assessed`.
4. **Produce overall assessment:**
   - Suite-level verdict.
   - Top findings ordered by impact.
   - Extension-specific mutation tool recommendation if the suite looks high-coverage but shallow (the extension names the tool).
5. **Emit a prioritized remediation worklist** (`P0` / `P1` / `P2` / `P3`) similar to the code-quality-review output.

---

## Per-Test Rubric

For every test case examined, emit these fields:

1. **Intent statement** — one sentence answering: *what requirement does this test encode?* If you cannot state one, say "no stateable intent" — that itself is a finding.
2. **Expected-value provenance** — how was the expected value chosen? One of:
   - `spec` — from a written spec, RFC, or standard.
   - `fixture` — from a versioned fixture file.
   - `domain-invariant` — derived from a domain rule (e.g. sum preserved under reorder).
   - `pasted-literal` — literal that looks pasted from an observed run; no external source.
   - `unknown` — cannot tell.
3. **Assertion target** — what is being checked?
   - `return-value` — the SUT's direct return.
   - `published-side-effect` — an outbound publish, write, or event that clients depend on.
   - `internal-mock-invocation` — a `verify(mock...)` on an owned collaborator.
   - `structural-shape` — only asserts that the result has certain fields/types, not their values.
   - `none` — no assertion.
4. **Smells matched** — list of codes from `references/smell-catalog.md` plus any loaded extension. Example: `HC-2, dotnet.HC-1`.
5. **Positive signals matched** — list of codes. Example: `POS-2, dotnet.POS-3`.
6. **Verdict** — `specification` / `characterization` / `ambiguous`.
7. **Severity** — `block` / `warn` / `info`.
8. **Recommended action** — one of: `rewrite-from-requirement` / `add-assertion` / `split` / `delete` / `keep`.

One finding per test. If a test hits multiple smells, cite them all in the codes list but pick the highest severity for the overall verdict.

---

## Output Format

### Per-test finding shape

```markdown
#### `TestFile.cs::Method_Scenario_Expected` (L42)

- **Intent:** Persists an order when checkout succeeds.
- **Provenance:** unknown — expected value is a pasted literal with no fixture link.
- **Assertion target:** internal-mock-invocation
- **Smells:** HC-5, HC-7, dotnet.HC-1
- **Positive signals:** —
- **Verdict:** characterization
- **Severity:** warn
- **Action:** rewrite-from-requirement. The test verifies `repo.Save` was called with a specific entity shape, not that the order was persisted as a client-observable outcome. Replace with an assertion on the returned order or on a state-query through the repository interface.
```

Severity rules:

- `block` — no assertions (HC-1), logic in the test (HC-4), tautology against trivial SUT (HC-2).
- `warn` — characterization verdict, interaction-only assertions on owned code, pasted-literal provenance on non-trivial expected values.
- `info` — low-confidence smells, ambiguous verdict, missing negative tests.

### Deep mode rollup

After all per-test findings, add:

```markdown
## Per-file rollup

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| `OrderServiceTests.cs` | 14 | 6 | 5 | 3 | HC-5, HC-7, dotnet.HC-1 | weak |
```

Then:

```markdown
## Suite assessment

- **Extensions loaded:** dotnet
- **Overall verdict:** <strong / adequate / weak / not assessed>
- **Top risks:** <3-5 bullets by impact>
- **Mutation testing recommendation:** <if suite looks shallow, name the tool from the extension>
- **Verification limits:** <what the static audit cannot determine>

## Prioritized remediation worklist

- **P0** — <work item, expected impact, effort estimate>
- **P1** — ...
```

---

## Rules

- **Assume the code under test may be wrong.** Never use observed SUT output as ground truth for the expected value of any assertion you are evaluating.
- **Test through the public API.** Do not flag missing tests for private methods, framework code, or pure delegation glue.
- **Mock invocation is a last resort.** Flag mocks of same-layer code; do not flag mocks of process boundaries (HTTP, database, filesystem, clock, message bus) unless the extension says otherwise.
- **A test without a stateable requirement is a characterization test regardless of appearance.** The intent statement is the gate.
- **Extensions augment, never override.** A carve-out suppresses a core smell only for the exact pattern it describes. When in doubt, prefer the core rule.
- **Be honest about the limits of static audit.** When git history, mutation results, coverage data, or spec links would change the verdict, say so and ask — do not fabricate certainty.
- **One finding per test.** Cite all matched smells in the codes list, but the overall verdict reflects the highest-severity smell.
- **Reward positive signals explicitly.** An audit that only complains gets tuned out.
- **Respect labeled characterization scaffolding.** A test file under a `characterization/`, `legacy/`, or `golden/` directory, or with a documented scaffold comment at the top, is *not* flagged as characterization smell — it is doing its job.
- **Stay read-only.** Do not modify tests or production code unless the user explicitly asks for fixes.

## Common Mistakes

Things the auditor itself must avoid:

- **Flagging snapshot tests whose output *is* the published contract** — API responses with a documented schema, RFC test vectors, locale-compiled message catalogs. Snapshots of *unspecified* rendered output are the smell; snapshots of *specified* output are positive.
- **Treating `verify(mock...)` as a smell when the verified call *is* the observable behavior** — publishing to a message bus, emitting an audit event, calling an outbound HTTP endpoint. When the mocked thing is a process boundary and the verified call is what a client observes, it is specification.
- **Calling a test tautological because the assertion duplicates a simple case** — when the test is actually a boundary case (`sum([]) == 0`, `reverse("") == ""`, `max([x]) == x`).
- **Demanding intent for clearly-labeled characterization scaffolding** in legacy modules.
- **Flagging fixture-equals-output as tautology** when the transformation is identity by specification (idempotency, round-trip).
- **Scoring a quality verdict from coverage alone.** Coverage is a floor, not a goal.
- **Running the full deep-mode workflow when the user only asked for a single-file audit.**
- **Fabricating certainty** about whether an expected value is correct per a real spec. When the provenance is unknown, say `unknown` — do not guess.
- **Stacking findings** — reporting ten low-severity smells on one test instead of the one finding that actually matters.
