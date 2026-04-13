---
name: test-quality-audit
description: Use when auditing unit, integration, or E2E test quality — distinguishing specification tests from characterization tests (unit), incidentally-scoped tests (integration), or scope-and-provenance failures (E2E), surfacing coupling, scope, and browser-layer smells, and producing per-test findings with severity and recommended actions. Dispatches on detected test type. Supports quick single-file / PR-diff audits and deep suite-wide audits, with pluggable per-stack extensions for framework-specific smells.
---

# Test Quality Audit

## Overview

Audit tests for quality. The skill supports three rubrics and dispatches on detected test type in step 0b of the workflow:

- **Unit rubric** — for unit tests and component tests. The central question for every test:

  > Could this test have been written from a stated requirement alone, without having seen the implementation?

  If no, it is a **characterization test** — it locks in current behavior, including any bugs present at the time of writing. A characterization test may still be valuable as legacy scaffolding, but it must be labeled as such. If it is unlabeled and sits alongside specification tests, it is a quality smell.

- **Integration rubric** — for in-process integration tests and out-of-process contract tests. The central question for every test:

  > What is this test integrating, and why couldn't a unit test prove the same thing?

  If the answer is *nothing new*, the test is a slower, flakier duplicate of a unit test and should be deleted or moved. If the answer is *everything at once*, the test is unfocused and has no defensible scope. A test labeled "integration" without a stateable scope is a smell regardless of how green it runs.

- **E2E rubric** — for browser-driven tests executed against a deployed or locally-launched stack through a real user agent. E2E tests inherit both failure modes above, amplified. Every E2E test must answer **two** central questions:

  > *(scope)* What user-observable outcome does this test prove, and why couldn't a cheaper test prove the same thing?
  >
  > *(provenance)* Could this test have been written from the user story alone, without observing what the UI currently does?

  A test that fails the scope question is slower and flakier than whatever lane it belongs in; move it down. A test that fails the provenance question is pinning current UI output — a screenshot of today's app wearing a test costume. The E2E rubric is split into four sub-lanes — **F** functional user journey, **A** accessibility audit, **P** performance budget, **S** security surface — because the failure modes are incompatible across the four.

The skill selects the rubric in [§ 0b (Rubric selection)](#0b-select-the-rubric) based on test type detection. The central question(s) applied to any given test is whichever set applies to the selected rubric.

**Before auditing, read the selected rubric:**

- For **unit rubric** audits: [../../docs/quality-reference/unit-testing.md](../../docs/quality-reference/unit-testing.md) — principles, full rationale, and the complete unit smell list.
- For **integration rubric** audits: [../../docs/quality-reference/integration-testing.md](../../docs/quality-reference/integration-testing.md) — principles, sub-lane distinctions (in-process vs out-of-process contract), and the complete integration smell list.
- For **E2E rubric** audits: [../../docs/quality-reference/e2e-testing.md](../../docs/quality-reference/e2e-testing.md) — principles, the four sub-lanes (F / A / P / S), and the complete E2E smell list.

Each document is the canonical rubric for its test type. This skill is the *workflow* for applying the selected rubric.

**Read `references/smell-catalog.md`** for the compact code list used in reports. The unit rubric uses `HC-*`, `LC-*`, `POS-*`; the integration rubric uses `I-HC-A*`, `I-HC-B*`, `I-LC-*`, `I-POS-*`; the E2E rubric uses `E-HC-F*`, `E-HC-A*`, `E-HC-P*`, `E-HC-S*`, `E-LC-*`, `E-POS-*`. Audit findings cite smells by code, not by restating them.

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
- **Run the extension's mutation-testing tool if it is installed** (see [references/procedures/mutation-testing.md](references/procedures/mutation-testing.md)). Skip gracefully with install instructions if it is not.

Trigger phrases: "full test audit", "audit the test suite", "deep test audit", "review all our tests", "find characterization tests".

**Default:** If the request is ambiguous, ask the user which mode they want.

---

## Extensions

The core rubric is framework-neutral. **Extensions** are per-stack smell packs loaded on demand that add framework-specific smells, positive signals, and carve-outs (explicit "do not flag" rules for patterns that look smelly in the core rubric but are idiomatic in a given framework).

Extensions live in `extensions/*.md`. Read `extensions/README.md` for the full convention, including the two supported file layouts (single-file vs. core + rubric addons). Current extensions:

- **.NET** — core + rubric addons. [extensions/dotnet-core.md](extensions/dotnet-core.md) is the shared core (detection, test-type dispatch, test doubles, rubric-neutral smells, SUT surface enumeration, determinism verification, Stryker mutation tool); [extensions/dotnet-unit.md](extensions/dotnet-unit.md), [extensions/dotnet-integration.md](extensions/dotnet-integration.md), and [extensions/dotnet-e2e.md](extensions/dotnet-e2e.md) are the rubric-specific addons. Covers xUnit, NUnit, MSTest, bUnit, Playwright .NET, Selenium, Moq, NSubstitute, FakeItEasy, FluentAssertions, Testcontainers, Stryker.NET.

### Detection phase (step 0 of every audit)

Before applying the rubric, detect which stacks are present in the audit target. Load every matching extension's **core file**; rubric addons are loaded later, after step 0b selects the rubric (see next subsection).

| Signal | Extension core to load |
|---|---|
| `*.csproj` or `*.sln` in the target; `xunit`, `nunit`, `mstest`, `Moq`, `NSubstitute`, `bunit`, `FluentAssertions`, `Microsoft.Playwright`, `Selenium.WebDriver` package refs | `extensions/dotnet-core.md` |
| `package.json` with `jest`, `vitest`, `mocha`, `@testing-library/*`, `@playwright/test`, `cypress`, `webdriverio` in devDependencies | *(future)* `extensions/javascript.md` |
| `pyproject.toml` or `setup.py` with `pytest` or `unittest` | *(future)* `extensions/python.md` |

Detection rules:

- **Multiple stacks** — load all matching extension cores. Note to the user which were loaded.
- **No matching extension** — proceed with the core rubric only. Note the missing extension as a limitation in the output; if the stack is common, recommend that a new extension be written.
- **Framework-specific grep hints** live in the extension, not here.

### Rubric addon loading (after step 0b)

After step 0b selects the rubric(s) for the audit target, load the matching rubric addon(s) for each loaded extension core:

| Rubric selected for a test / file / project | `dotnet` addon to load |
|---|---|
| `unit` (or `component`) | `extensions/dotnet-unit.md` |
| `integration` | `extensions/dotnet-integration.md` |
| `e2e` | `extensions/dotnet-e2e.md` |

For a mixed-rubric audit target (e.g. a test project containing both unit and integration tests), load every addon that at least one test in the target needs. Single-file extensions (those that ship `<stack>.md` only, with no addons) have no addon load step — they are fully resident after the detection phase. See `extensions/README.md` for the full convention.

### Precedence

Extensions **never override** core rules. They can:

- **Add** new smells (namespaced as `<ext>.HC-N`, `<ext>.LC-N`, `<ext>.POS-N`).
- **Carve out** core smells that would produce false positives in idiomatic framework patterns. A carve-out is an explicit `do not flag HC-X when <pattern>`.

If a carve-out and a core smell conflict, the carve-out wins *only* for the exact pattern described. When in doubt, prefer the core rule.

---

## Quick Mode Workflow

0. **Detect stack and load extensions.** Glob for project manifests (`*.csproj`, `package.json`, `pyproject.toml`, etc.) within the audit target. Read matching extension files. Announce which extensions loaded.

### 0b. Select the rubric.

After loading extensions, detect whether the audit target contains unit tests, integration tests, E2E tests, or a mix. Use the `Test type detection signals` section of each loaded extension. Signals in order of precedence:

1. **Explicit user instruction.** If the user said "audit the integration tests in X" or "audit the E2E tests" or equivalent, use that.
2. **Project-level signal.**
   - Project name containing `E2E`, `EndToEnd`, or matching a browser-test convention; OR a `<PackageReference>` on a browser automation library (e.g. `Microsoft.Playwright`, `Selenium.WebDriver`, `@playwright/test`, `cypress`, `webdriverio`) routes the project to the **E2E** rubric.
   - Project name containing `Integration`, `IntegrationTests`, or equivalent; OR a transitive `<ProjectReference>` closure that names the integration-rubric SDK for the stack (e.g. `Microsoft.NET.Sdk.Web` for .NET) routes the project to the **integration** rubric.
   - A `*.Tests` project with no E2E or integration markers defaults to **unit**.
3. **File-level signal (from extension).**
   - Imports of a browser-automation namespace — `using Microsoft.Playwright;`, `using OpenQA.Selenium;`, `import { test } from '@playwright/test';`, `require('cypress')`, or equivalent — route the file to the **E2E** rubric.
   - Presence of integration-routing types such as (for .NET) `WebApplicationFactory<T>`, `HostBuilder`, `TestServer`, `DistributedApplicationTestingBuilder`, Testcontainers clients, or emulator-endpoint `CosmosClient` / `BlobServiceClient` instances routes the file to the **integration** rubric.
4. **Test-level signal.** A mixed file's tests are classified per-test using the same extension signals.
5. **E2E sub-lane signal.** Once a test has been routed to E2E, classify it into a sub-lane (F / A / P / S) using its trait / category / tag metadata and body contents. Signals in order of precedence:
   - `[Trait("Category", "Accessibility")]`, `@tag("a11y")`, axe-core / `AccessibilityHelper` / `AxeBuilder` import → sub-lane **A**.
   - `[Trait("Category", "Perf")]`, `@tag("perf")`, Web Vitals / `PerformanceObserver` / `PerfHelper` / Lighthouse import → sub-lane **P**.
   - `[Trait("Category", "Security")]`, `@tag("security")`, assertions on CSP / CORS / cookie attributes / cross-origin iframe blocking / tampered-cookie behaviour → sub-lane **S**.
   - Otherwise → sub-lane **F** (functional user journey). This is the default when no other sub-lane signal fires.
6. **Fallback.** If none of the above apply clearly, ask the user which rubric (and, for E2E, which sub-lane) to apply. Do not guess.

Record which rubric (and, for E2E, which sub-lane) was selected for each project / file / test. Deep-mode output includes this record in the suite assessment so the reader can audit the dispatch itself.

1. **Identify the target.** A file, a glob, a set of changed files from a PR diff, or a single test within a file.
2. **Read supporting infrastructure first.** Before reading individual tests, Read any test base classes, fixtures, or custom helpers referenced by the target tests. This prevents flagging idiomatic uses of a helper as a smell.
3. **For each test case:**
   - Read the test body.
   - Apply the per-test rubric (see below) using the fields for the rubric selected in step 0b. Apply core smells first, then extension smells (filtered by `Applies to:`), then extension carve-outs.
   - Determine the verdict, severity, and recommended action.
4. **Emit findings** in the per-test shape (see Output format below). Annotate each finding with the rubric it was evaluated under. Do not restate smell descriptions — cite codes and let the reader look them up.

## Deep Mode Workflow

0. **Detect stack and load extensions.** Same as quick mode. Record which extensions loaded so the final report can disclose them.

0b. **Select the rubric.** Same as quick mode — see [§ 0b (Rubric selection)](#0b-select-the-rubric) above. Record the selection per project / file / test for the suite assessment in step 5.

1. **Establish scope.** Confirm which test projects or directories. The rubric selection from step 0b determines whether each test is audited under the unit or integration rubric; the user does not need to pre-declare test type. If the target mixes unit and integration tests, the audit applies each rubric to its matching tests and notes the dispatch in the output.
2. **Enumerate test files.** Use Glob with the extension's file patterns. For each file:
   - Skim for infrastructure (fixtures, test bases, custom helpers) before reading individual tests.
   - Apply the per-test rubric (core + loaded extensions) to each test case.
2.5. **SUT surface enumeration (gap detection — deep mode only).** See [references/procedures/sut-surface-enumeration.md](references/procedures/sut-surface-enumeration.md) for the full procedure. In short:
   - **Identify the SUT** for each audited test project by walking its `<ProjectReference>` closure (or the equivalent module import graph for non-.NET stacks) to the production project(s) under test.
   - **Enumerate the testable surface** by running the stack extension's grep patterns against the SUT source tree: public methods / types, HTTP routes and function handlers, migration classes, throw sites, validation attributes. Extensions own the patterns; this file owns the workflow and the gap-report format.
   - **Cross-reference against test discovery.** A symbol covered by the test project is one whose name (or, for routes and migrations, whose registration string / class name) appears in at least one test method name or test body.
   - **Emit a gap report** (see [references/procedures/sut-surface-enumeration.md § Gap report format](references/procedures/sut-surface-enumeration.md#gap-report-format)) listing unreferenced entries. Each entry is a *probable* gap — grep is an approximate cross-reference — and the report says so explicitly.
   - **Skip gracefully** when the stack extension has no **SUT surface enumeration** section. Record the skip reason and continue with static findings only. Never hard-fail the audit on a missing extension section.
   - **Quick mode does not run this step.** Gap detection is suite-level; running it on a single-file or PR-diff target would produce nonsense. Step 2.5 fires only in deep mode.
2.6. **Auth/authz matrix coverage (deep mode only).** See [references/procedures/auth-matrix-enumeration.md](references/procedures/auth-matrix-enumeration.md). In short:
   - **Enumerate protected endpoints** by matching the extension's auth-attribute patterns (e.g. `[Authorize]`, `[HttpTrigger(AuthorizationLevel.Function)]`, `authorize(...)` middleware). Each protected endpoint is a row in the matrix.
   - **Define the matrix columns** as the minimum set of auth scenarios the audit expects: `anonymous`, `token-expired`, `token-tampered`, `insufficient-scope`, `sufficient-scope`, `cross-user`. Extensions may extend this list (e.g. `admin-only` for admin endpoints).
   - **Cross-reference** each endpoint × scenario cell against the test project. A cell is covered when at least one test exercises that endpoint with that auth scenario and asserts the documented response (401, 403, 200, etc.).
   - **Emit gap rows** as `Gap-AuthZ` entries in the gap report. Each row names the endpoint, the uncovered cells, and a confidence annotation.
   - **Skip gracefully** when the extension has no auth-matrix section. Auth enumeration is optional per stack.
2.7. **Migration upgrade-path enumeration (deep mode only).** See [references/procedures/migration-upgrade-path.md](references/procedures/migration-upgrade-path.md). In short:
   - **Enumerate migration classes** via the extension's migration grep pattern (e.g. `api/Migrations/*.cs` for this repo).
   - **Check for an upgrade-path test** for each migration: a test that (a) references the migration class name and (b) arranges non-empty seed data representing the N-1 schema state and (c) asserts the post-migration state. A migration test that runs against an empty database is a smell (`I-HC-A7`) but also a failed upgrade-path test under this step.
   - **Emit gap rows** as `Gap-MigUpgrade` entries in the gap report. For a repo with an expand-only migration rule (see `CLAUDE.md`), every migration should have an upgrade-path test.
   - **Skip gracefully** when the extension has no migration-upgrade section or the repo has no migrations directory.
3. **Roll up per-file:**
   - Total tests.
   - Verdict counts: specification / characterization / ambiguous.
   - Top smells by frequency.
   - File-level quality: `strong` / `adequate` / `weak` / `not assessed`.
4. **Mutation testing (conditional).** See [references/procedures/mutation-testing.md](references/procedures/mutation-testing.md) for the full procedure. In short:
   - Read the loaded extension's **Mutation tool** section.
   - Run the extension's **detection command** to check whether the tool is installed.
   - **If installed** — run it against the SUT per the extension's instructions, capture the score and surviving-mutant details, and incorporate the findings into step 5 below. If the run fails for reasons the extension documents as known SUT limitations (e.g. Blazor WASM), record the failure reason and continue with static findings only.
   - **If not installed** — skip the mutation run and record the skip reason. Include the extension's install instructions in the step-6 output so the user can enable it for a future audit.
   - **Never hard-fail the audit on a mutation step failure.** Static findings stand on their own.
4.5. **Determinism verification (optional, deep mode only, gated on project size).** See [references/procedures/determinism-verification.md](references/procedures/determinism-verification.md).
   - **Rationale:** per-test smells like `HC-11` / `I-HC-A5` / `E-HC-F3` flag *suspected* flake. Running the suite twice proves flake from runtime evidence.
   - **Gating:** run only when the audited test project has < 500 test methods *and* the user opts in for the session *or* the extension declares the suite as cheap-to-rerun. Skip for larger suites with a note recommending a targeted rerun of the top-N slowest tests instead.
   - **Procedure:** run the non-E2E test project(s) twice in sequence via the extension's cheap-rerun command (for .NET: `dotnet test --no-build --verbosity quiet`). Compare the pass / fail / error list between the two runs.
   - **On difference**, flag each diverged test as a runtime-proven determinism finding. A diverged test is a stronger signal than a static `HC-11` smell — it is evidence, not suspicion.
   - **Skip gracefully** when the extension has no cheap-rerun command, when the suite is too large, or when prior steps of the audit already produced enough static determinism findings to act on.
5. **Produce overall assessment:**
   - Suite-level verdict.
   - Top findings ordered by impact.
   - **Audit-vs-mutation reconciliation** — if step 4 produced results, identify files where the static verdict and the mutation findings disagree. These are the highest-signal findings: a file rated `strong` by static audit but with surviving mutants reveals test-quality gaps neither method alone would catch.
   - **No-coverage discoveries** — if the mutation tool surfaced entire files with zero test coverage, list them. The static audit cannot see these because it only examines files that already have tests.
   - **Gap report** — the output of step 2.5. List probable gaps by category (public API / routes / migrations / throw sites / validation attributes). When both step 2.5 and step 4 produced results, **reconcile** them: a symbol flagged as a probable gap by step 2.5 *and* appearing as a `NoCoverage` file in the mutation report is a **confirmed gap**; static-only probable gaps are weaker signal; mutation-only gaps (no grep match but covered at runtime) are a sign the step 2.5 grep patterns in the extension need tuning.
   - **Pyramid ratio** — count tests per rubric across the audited scope and report the distribution. Google's default guidance is roughly 70–80 % unit, 15–20 % integration, ≤10 % E2E (*Software Engineering at Google* ch. 11 on test sizing). Flag as a design finding when the inversion is sharp: unit < 60 % or E2E > 15 %. An inverted pyramid is a cost signal, not a per-test smell — it usually means unit-lane coverage is weak and teams compensated by writing expensive tests at the top.
   - **Runtime distribution** — if any `.trx` / JUnit XML test result file is available in the repo (glob: `**/TestResults/*.trx`, `**/test-results/*.xml`), parse it and extract the top-10 slowest tests with their elapsed time. Flag any unit test > 100 ms and any in-process integration test > 2 s for slow-test review; do not gate on this, just report. Skip silently if no test-result files are present.
   - **Determinism findings** — if step 4.5 ran, list any test that diverged between the two runs. Each diverged test is a runtime-proven flake finding; annotate with the suspected cause from the static smells if one was detected.
6. **Emit a prioritized remediation worklist** (`P0` / `P1` / `P2` / `P3`) similar to the code-quality-review output. Worklist items derived from mutation findings should be tagged `[mutation]` so they're distinguishable from purely-static findings.

---

## Mutation testing (conditional)

Runs as step 4 of the deep-mode workflow when the loaded extension declares a mutation tool and the tool is installed. Static audit grades *test quality*; mutation testing grades *test effectiveness*. The highest-signal finding is a file rated `strong` by static audit that nevertheless has surviving mutants — that disagreement lives in the audit-vs-mutation reconciliation.

**Read [references/procedures/mutation-testing.md](references/procedures/mutation-testing.md) before running step 4.** It documents the full procedure (detection command → SUT shape check → run → parse) and the three output states (A: ran successfully, B: tool not installed, C: attempted and failed). Never run in quick mode. Never run against E2E audit targets.

---

## SUT surface enumeration

Runs as step 2.5 of the deep-mode workflow. This is the skill's static gap-detection pass: *find tests that don't exist yet* for public API, HTTP routes, migrations, throw sites, and validation attributes in the SUT. Complementary to mutation testing — surface enumeration catches code with no test at all; mutation testing catches tests that execute code without verifying it.

**Read [references/procedures/sut-surface-enumeration.md](references/procedures/sut-surface-enumeration.md) before running step 2.5.** It documents the full procedure (read extension patterns → identify SUT → enumerate → cross-reference → emit gap report), the five gap classes (`Gap-API`, `Gap-Route`, `Gap-Migration`, `Gap-Throw`, `Gap-Validate`), the rules (suite-level only, probable not confirmed, reconcile with mutation testing), and the two gap-report output states. Never run in quick mode. Never run against E2E audit targets.

---

## Auth matrix enumeration

Runs as step 2.6 of the deep-mode workflow. Enumerates protected endpoints in the SUT and checks each cell of the auth scenario matrix (`anonymous`, `token-expired`, `token-tampered`, `insufficient-scope`, `sufficient-scope`, `cross-user`) for test coverage. Per-endpoint signal — fires when a test is *missing* for a scenario. Complementary to core smells `I-HC-B7` / `E-HC-S3` which fire when a test is *there and incomplete*.

**Read [references/procedures/auth-matrix-enumeration.md](references/procedures/auth-matrix-enumeration.md) before running step 2.6.** It documents the matrix columns, the cross-reference procedure, and the `Gap-AuthZ` output format. Runs only in deep mode. Integration-rubric scope. Skipped when the loaded extension has no auth-matrix section.

---

## Migration upgrade-path enumeration

Runs as step 2.7 of the deep-mode workflow. For each database migration in the SUT, checks that an upgrade-path test exists that runs the migration against representative prior-schema seed data (not an empty database). Enforces the "expand-only" migration rule: a migration that breaks existing queries fails its upgrade-path test. Complementary to core smell `I-HC-A7` which flags empty-DB migration tests.

**Read [references/procedures/migration-upgrade-path.md](references/procedures/migration-upgrade-path.md) before running step 2.7.** It documents the three-condition detection (references migration class + seeds non-empty state + asserts post-state) and the `Gap-MigUpgrade` output format. Runs only in deep mode. Integration-rubric scope. Skipped when the loaded extension has no migration-upgrade section.

---

## Determinism verification

Runs as step 4.5 of the deep-mode workflow. Runs the non-E2E test suite twice in sequence and compares the pass / fail list. A test that passes once and fails once is a runtime-proven flake — stronger evidence than any static smell. Converts suspicion (`HC-11`, `LC-5`, `I-HC-A5`, `I-HC-A11`, `E-HC-F3`) into evidence.

**Read [references/procedures/determinism-verification.md](references/procedures/determinism-verification.md) before running step 4.5.** It documents the gating rules (suite size < 500 methods, run-1 under 60 s, user opt-in), the two-run diff procedure, and the `Determinism findings` output section. Optional. Skipped when the extension has no cheap-rerun command or when the suite is too large. Never run against E2E projects.

---

## Per-Test Rubric

Use the **unit rubric fields** below when the rubric selection in step 0b is `unit` (or `component`, which is unit-equivalent). Use the **integration rubric fields** below when it is `integration`. Use the **E2E rubric fields** below when it is `e2e`. Each test gets a single finding under a single rubric.

### Unit rubric per-test fields

For every unit test case examined, emit these fields:

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
4. **Test size** (Google sizing) — `small` (single process, no I/O, no sleep, no real clock), `medium` (single machine, localhost only, no external network), `large` (multi-machine). Unit tests should be `small` by construction; a unit test that touches filesystem, network, or the real clock is a candidate for lane migration. Flag `LC-7` or extension slow-unit-test smell (`dotnet.LC-5`) when size > `small`.
5. **Smells matched** — list of codes from `references/smell-catalog.md` (unit section) plus any loaded extension. Example: `HC-2, dotnet.HC-1`.
6. **Positive signals matched** — list of codes. Example: `POS-2, dotnet.POS-3`.
7. **Verdict** — `specification` / `characterization` / `ambiguous`.
8. **Severity** — `block` / `warn` / `info`.
9. **Recommended action** — one of: `rewrite-from-requirement` / `add-assertion` / `split` / `delete` / `keep` / `move-to-integration-lane`.

### Integration rubric per-test fields

For every integration test case examined, emit these fields. Derived from [integration-testing.md § 9](../../docs/quality-reference/integration-testing.md).

1. **Scope statement** — one sentence answering: *what seam does this test exercise, and why couldn't a unit test prove the same thing?* If you cannot state one, say "no stateable scope" — that is itself a finding (and the central problem of the integration rubric).
2. **Sub-lane** — `A` (in-process, real modules wired together with real adjacent dependencies) or `B` (out-of-process contract, SUT exercised as a deployed artifact through its public protocol).
3. **Test size** (Google sizing) — `small` (single process, no I/O), `medium` (single machine, localhost only, no external network), `large` (multi-machine). Sub-lane A is almost always `medium`.
4. **Seam narrowness** (Fowler) — `narrow` (one SUT, one real dependency, one seam — the default) or `broad` (multiple real dependencies interacting — must be justified).
5. **Fixture and data provenance** — how was the test's state set up? One of:
   - `per-test-factory` — each test creates its own state with unique keys.
   - `shared-immutable` — a shared fixture that no test mutates (acceptable).
   - `shared-mutable` — a shared fixture that tests mutate (a smell — likely `I-HC-A2` or `I-HC-A4`).
   - `external-snapshot` — pinned to a snapshot / golden file / contract document.
   - `pasted-literal` — expected value pasted from a run with no provenance.
   - `unknown` — cannot tell.
6. **Assertion target** — what is being checked?
   - `published-contract` — a status code, error envelope, audit event schema, OpenAPI fragment, or consumer pact with a cited source.
   - `observable-state` — the row that landed, the message that was published, the entity that was persisted.
   - `response-value` — the SUT's HTTP or protocol-level response.
   - `internal-mock-invocation` — a `verify(mock...)` on an owned collaborator (almost always a smell inside an integration test; likely `I-HC-A1` or `I-HC-B5`).
   - `structural-shape` — only asserts that the response has certain fields, not their values.
   - `none` — no assertion.
7. **Smells matched** — list of codes from `references/smell-catalog.md` (integration section) plus any loaded extension entries with `Applies to: integration` or `Applies to: unit, integration`. Example: `I-HC-A2, I-HC-A5, dotnet.I-HC-A1`.
8. **Positive signals matched** — list of codes. Example: `I-POS-3, I-POS-6, dotnet.POS-3`.
9. **Verdict** — `specification` / `incidental` / `ambiguous`. `incidental` is the integration-rubric analogue of the unit rubric's `characterization` — a test that sweeps a lot of code through execution without a defensible scope.
10. **Severity** — `block` / `warn` / `info`.
11. **Recommended action** — one of: `rewrite-from-requirement` / `add-assertion` / `split` / `narrow-the-seam` / `move-to-unit-lane` / `replace-with-contract-test` / `delete` / `keep`.

### E2E rubric per-test fields

For every E2E test case examined, emit these fields. Derived from [e2e-testing.md § 9](../../docs/quality-reference/e2e-testing.md).

1. **User-outcome statement** — one sentence answering: *what does the user accomplish or experience through this test?* If you cannot state one, say "no stateable user outcome" — that itself is a finding, and the provenance question has failed.
2. **Scope statement** — one sentence answering: *why couldn't a cheaper test (unit or integration) prove the same thing?* If you cannot state one, say "no stateable scope" — that itself is a finding, and the scope question has failed.
3. **Sub-lane** — `F` (functional user journey), `A` (accessibility audit), `P` (performance budget), or `S` (security surface).
4. **Test size** (Google sizing) — E2E is almost always `large` (multi-process, real browser, real backend).
5. **Selector provenance** — how does the test locate UI elements? One of:
   - `accessible-name` — locators derived from accessible role, accessible name, visible text, label association, or placeholder.
   - `test-id-with-accessible-fallback` — dedicated test-id attribute, used only when no accessible name is available.
   - `test-id-only` — test-id with no accessible-name fallback even when one exists.
   - `css-or-xpath` — raw CSS selectors, xpath, or deep DOM-path selectors.
   - `mixed` — test uses more than one of the above.
6. **Wait strategy** — one of:
   - `condition-based` — waits only on observable state (visibility, URL, response, predicate).
   - `mixed` — combination of condition-based and wall-clock waits.
   - `wall-clock` — sleeps, fixed-duration delays, fixed-budget waits, retry-until-green loops.
   - `none` — no explicit waits (may indicate the test races the UI).
7. **Fixture and data provenance** — how is the test's state set up? One of:
   - `per-test-factory` — each test creates its own user, data, and browser context.
   - `shared-immutable` — a shared fixture no test mutates (acceptable).
   - `shared-mutable` — a shared fixture tests mutate (a smell — likely `E-HC-F5`).
   - `external-snapshot` — pinned to a snapshot / baseline / recorded payload.
   - `pasted-literal` — expected value pasted from a run with no provenance.
   - `unknown` — cannot tell.
8. **Assertion target** — what is being checked?
   - `user-observable-outcome` — a user-verifiable state: reached page, visible content, submitted form, completed task.
   - `url-only` — only a URL match, no content check.
   - `element-presence-only` — only that an element exists, not what it says or does.
   - `snapshot` — DOM snapshot, golden file, serialized output.
   - `pixel-baseline` — visual regression.
   - `published-contract` — a WCAG rule set, Web Vitals threshold, CSP policy, or similar cited external source (positive for sub-lanes A/P/S).
   - `none` — no assertion.
9. **Smells matched** — list of codes from `references/smell-catalog.md` (E2E section) plus any loaded extension entries with `Applies to: e2e` or `Applies to: e2e, ...`. Example: `E-HC-F3, E-HC-F5, dotnet.E-HC-F1`.
10. **Positive signals matched** — list of codes. Example: `E-POS-2, E-POS-6, dotnet.POS-3`.
11. **Verdict** — `specification` / `characterization` / `incidental` / `ambiguous`. The unit rubric's `characterization` and the integration rubric's `incidental` both apply at the E2E layer: the first for tests that pin observed UI output (provenance failure), the second for tests with no defensible scope (scope failure). A test can fail either; cite the dominant failure in the verdict.
12. **Severity** — `block` / `warn` / `info`.
13. **Recommended action** — one of: `rewrite-from-user-story` / `add-assertion` / `split` / `narrow-the-journey` / `move-to-integration-lane` / `move-to-unit-lane` / `replace-selector-strategy` / `replace-wait-strategy` / `delete` / `keep`.

One finding per test, under exactly one rubric. If a test hits multiple smells, cite them all in the codes list but pick the highest severity for the overall verdict.

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

Severity rules (apply to all three rubrics; smell codes differ):

- `block` — no assertions (`HC-1` / `I-HC-A10` / `E-HC-F1`), logic in the test (`HC-4`), tautology against trivial SUT (`HC-2`), every-dependency-mocked "integration" test (`I-HC-A1`), downstream service mocked at the transport layer (`I-HC-B5`), test hits a test-only endpoint that does not exist in production (`I-HC-B2` / `E-HC-S5`), E2E test that re-proves an integration contract (`E-HC-F10`), E2E security test that asserts only server headers without browser enforcement (`E-HC-S1`).
- `warn` — characterization verdict (unit) or incidental verdict (integration or E2E), interaction-only assertions on owned code, pasted-literal provenance on non-trivial expected values, shared mutable fixture (`I-HC-A2`, `I-HC-A4`, `E-HC-F5`), wall-clock waits (`I-HC-A5`, `E-HC-F3`), fragile setup (`I-HC-A11`, `E-HC-F11`), log-text assertion (`HC-6` / `I-HC-A6`), migration test against empty DB (`I-HC-A7`), auth test with only the happy path (`I-HC-B7` / `E-HC-S3`), implementation selectors in an E2E test (`E-HC-F2`), DOM snapshot or pixel baseline with no workflow (`E-HC-F7`, `E-HC-F8`), axe with no WCAG level (`E-HC-A5`), axe rule suppression with no justification (`E-HC-A1`), perf budget with no external source (`E-HC-P1`), single-sample perf assertion (`E-HC-P2`), localhost-only perf budget (`E-HC-P4`).
- `info` — low-confidence smells (`LC-*` / `I-LC-*` / `E-LC-*`), ambiguous verdict, missing negative tests.

### Example integration finding

```markdown
#### `OrdersApiTests.cs::Get_Orders_Returns_Seeded_Rows` (L48)

- **Rubric:** integration
- **Scope:** "an integration test for the orders endpoint" — no stateable scope; depends on order from the previous test.
- **Sub-lane:** A (in-process)
- **Test size:** medium
- **Seam narrowness:** broad — exercises HTTP pipeline + DI container + real-database emulator without isolating any one seam.
- **Fixture/data provenance:** shared-mutable — shared in-process host factory via class fixture, no per-test data scoping.
- **Assertion target:** response-value (asserts a global row count)
- **Smells:** I-HC-A2, I-HC-A4, I-LC-6, dotnet.I-HC-A1
- **Positive signals:** —
- **Verdict:** incidental
- **Severity:** warn
- **Action:** narrow-the-seam. Rewrite so each test owns its data with a unique tenant id or partition key; assert on the specific rows the test created, not a global count. See `I-POS-4` (per-test data ownership).
```

### Example E2E finding

```markdown
#### `SignInJourney.cs::Login_Flow_Works` (L32)

- **Rubric:** e2e
- **User-outcome:** "no stateable user outcome" — test name describes UI steps, not a user goal.
- **Scope:** "sign-in integration" — but the assertion is on a URL match only; an integration test of the auth endpoint would prove the same thing without a browser.
- **Sub-lane:** F (functional user journey)
- **Test size:** large
- **Selector provenance:** css-or-xpath — locates the button via a CSS class path that mirrors the current layout.
- **Wait strategy:** wall-clock — `Task.Delay(2000)` after clicking the sign-in button, then a URL-only assertion.
- **Fixture/data provenance:** shared-mutable — browser context reused across tests in the fixture.
- **Assertion target:** url-only
- **Smells:** E-HC-F1, E-HC-F2, E-HC-F3, E-HC-F5, E-HC-F6, E-HC-F10, E-LC-2
- **Positive signals:** —
- **Verdict:** characterization
- **Severity:** block
- **Action:** rewrite-from-user-story. Rename to `Anonymous_User_Signs_In_And_Reaches_Authenticated_Home`. Replace the CSS selector with an accessible-role locator for the sign-in button. Replace the sleep with a condition-based wait on the authenticated-home landmark. Assert on a user-observable element on the authenticated page (e.g. the user's display name). Give each test its own browser context. Consider whether the scope is actually integration-lane — an integration test of the auth callback endpoint would prove the contract cheaper.
```

### Deep mode rollup

After all per-test findings, emit a per-file rollup table, a suite-level assessment block (pyramid ratio, gap report, runtime distribution, determinism findings, mutation testing subsection), and a prioritized remediation worklist.

**Read [references/procedures/deep-mode-output-format.md](references/procedures/deep-mode-output-format.md) before producing step-5 / step-6 output.** It carries the full templates for `## Per-file rollup`, `## Suite assessment`, and `## Prioritized remediation worklist`, with pointers back to the three sub-procedures that own individual subsections (SUT surface enumeration owns `### Gap report`, determinism verification owns `### Determinism findings`, mutation testing owns `### Mutation testing`).

---

## Rules

### Rules common to both rubrics

- **Assume the code under test may be wrong.** Never use observed SUT output as ground truth for the expected value of any assertion you are evaluating.
- **Test through the public API.** Do not flag missing tests for private methods, framework code, or pure delegation glue.
- **Extensions augment, never override.** A carve-out suppresses a core smell only for the exact pattern it describes. When in doubt, prefer the core rule.
- **Respect extension `Applies to:` tags.** Only cite an extension smell in a finding when the selected rubric matches the smell's `Applies to:` field (or the field is absent, which defaults to `unit`).
- **Be honest about the limits of static audit.** When git history, coverage data, flake history, spec links, or contract pacts would change the verdict, say so and ask — do not fabricate certainty.
- **Run mutation testing in deep mode when the tool is installed.** Check availability via the extension's detection command. If installed, run it per the extension; if not, skip and report install instructions. If installed but the SUT shape matches a documented limitation (e.g. Blazor WASM for Stryker.NET), skip with the documented reason and workaround. **Never hard-fail the audit on a mutation step failure** — static findings stand on their own. Mutation testing applies to both unit and in-process integration SUTs.
- **Mutation findings augment, never override, static findings.** A surviving mutant in a file the static audit rated `strong` is a meaningful disagreement worth investigating, but it does not automatically downgrade the file to `weak`. Report it as a reconciliation finding, not a verdict change.
- **Run SUT surface enumeration in deep mode when the extension has the section.** See [references/procedures/sut-surface-enumeration.md](references/procedures/sut-surface-enumeration.md). Check whether the loaded extension declares grep patterns for gap classes. If yes, run step 2.5. If no, record the skip reason and continue. **Never run surface enumeration in quick mode** — gap detection is suite-level and produces noise on single-file or PR-diff targets.
- **Gap findings are probable, not confirmed.** Grep is an approximate cross-reference: a public method covered indirectly via its caller will look untested. Each gap entry must carry a confidence annotation (`high` / `medium`) and a recommendation to verify via mutation testing or manual read before acting.
- **One finding per test, under one rubric.** A test is either unit or integration, never both. Cite all matched smells in the codes list, but the overall verdict reflects the highest-severity smell under the selected rubric.
- **Reward positive signals explicitly.** An audit that only complains gets tuned out.
- **Stay read-only.** Do not modify tests or production code unless the user explicitly asks for fixes. Running a mutation tool is allowed because it does not modify checked-in files; its output directory (e.g. `StrykerOutput/`) should already be in `.gitignore` or flagged as needing to be added.

### Rules specific to the unit rubric

- **Mock invocation is a last resort.** Flag mocks of same-layer code; do not flag mocks of process boundaries (HTTP, database, filesystem, clock, message bus) unless the extension says otherwise.
- **A test without a stateable requirement is a characterization test regardless of appearance.** The intent statement is the gate.
- **Respect labeled characterization scaffolding.** A test file under a `characterization/`, `legacy/`, or `golden/` directory, or with a documented scaffold comment at the top, is *not* flagged as characterization smell — it is doing its job.

### Rules specific to the integration rubric

- **Every integration test must state what it is integrating and why a unit test wouldn't do.** A test without a stateable scope is a smell regardless of how green it runs. The scope statement is the gate.
- **Prefer narrow over broad integration tests.** One SUT, one real dependency, one seam. Broad tests must justify their breadth in their name or a comment.
- **Inside an integration test, a mock is a scope leak.** Justify it (process or deployment boundary, no contract available) or move the test to the unit lane. The proof obligation for every integration-test mock is "why couldn't a contract test cover this?"
- **At a service boundary, prefer a consumer-driven contract over a mock.** A passing contract test subsumes a mock.
- **Test data is owned per-test.** Shared mutable fixtures are a smell (`I-HC-A2` / `I-HC-A4`).
- **Log and trace assertions are valid only against published contracts.** Free-text logs are not contracts. Structured audit events with a documented schema are.
- **Flake is a scope signal, not a quarantine problem.** Quarantining flakes hides underlying scope confusion.

### Rules specific to the E2E rubric

- **Every E2E test must state both its user outcome and its scope.** A test that fails either question (provenance or scope) is a smell regardless of how green it runs. The user-outcome statement and the scope statement are both gates.
- **Default to no.** An E2E test is the slowest, flakiest, most expensive test in the suite. Route coverage to unit or integration first; promote to E2E only when the user outcome genuinely requires a browser.
- **Name tests as user stories, not UI steps.** A test named for clicks and keystrokes is pinning the UI. A test named for what the user accomplishes is specifying a journey. The name is the provenance check.
- **Use accessible-name locators.** Role, accessible name, visible text, label, placeholder. Implementation selectors (CSS class paths, xpath, internal ids) are characterization in disguise — flag as `E-HC-F2`.
- **Condition-based waits only.** Wall-clock sleeps and fixed-budget waits are flake in slow motion at the E2E layer, doubly disqualifying because browsers expose real state predicates. Flag as `E-HC-F3`.
- **Per-test browser context.** Shared cookies, localStorage, sessionStorage, or service workers across tests are the E2E analogue of shared mutable fixtures and fail in the same way. Flag as `E-HC-F5`.
- **Snapshot equals characterization, amplified.** DOM snapshots (`E-HC-F7`) and pixel baselines (`E-HC-F8`) are valid only with a review gate, an owner, and a drift process. Without that scaffolding they pin today's build.
- **Perf budgets cite an external source.** Web Vitals thresholds, RUM baselines, or team SLOs. A threshold with no source is a pasted literal (`E-HC-P1`).
- **A11y audits cite a WCAG conformance level.** "Passes axe" without a level is not a contract (`E-HC-A5`). Rule suppressions carry linked justifications (`E-HC-A1`).
- **Security tests exercise browser enforcement.** Header-value assertions belong in integration sub-lane B. Sub-lane S exists to prove the browser does what the header claims. Flag as `E-HC-S1`.
- **Quarantine is a scope signal, not a fix.** A growing quarantine list is a design finding for the suite, not a backlog item.
- **Traces and screenshots captured on failure are diagnostics, not assertions.** Do not flag them as `E-HC-F7` when they are only written on failure and never consumed by test logic.

## Common Mistakes

Things the auditor itself must avoid:

- **Applying the unit rubric to an integration test.** A test that constructs `WebApplicationFactory<T>` (or equivalent) and exercises a real HTTP seam through real middleware is not a unit test, regardless of project location. Route it to the integration rubric in step 0b and apply the scope-based central question. If the dispatch signals are ambiguous, ask the user — do not default to unit.
- **Applying the integration rubric to a unit test.** A test that constructs the SUT directly with mocked dependencies (`new OrderService(mockRepo.Object, ...)`) is a unit test even if it lives in a project named `*Integration.Tests`. Route it to the unit rubric.
- **Applying the integration rubric to an E2E test.** A test that drives a real browser through a real user agent (imports `Microsoft.Playwright`, `OpenQA.Selenium`, `@playwright/test`, `cypress`, or similar) is an E2E test, not an integration test, regardless of project name. Route it to the E2E rubric, then classify into sub-lane F / A / P / S. A Playwright test that asserts only server-side response headers with no browser-side enforcement check is a misrouted integration test — flag `E-HC-S1` and recommend moving to integration sub-lane B.
- **Applying the E2E rubric to an integration contract test.** A test that asserts an HTTP status code, error envelope shape, or JSON response body through a network call but does *not* use a browser belongs in integration sub-lane B, not the E2E lane. Route to the integration rubric.
- **Confusing E2E sub-lane S with an integration-lane security check.** A security test that only asserts response headers or cookie attributes at the HTTP level is integration sub-lane B work, not E2E. Sub-lane S exists to prove the browser *enforces* the policy (CSP blocks an injected script, the cookie jar honours `SameSite`, an iframe is blocked by `X-Frame-Options`). If the assertion does not need a browser, it is in the wrong lane.
- **Flagging heavy setup as `LC-7` when the test is correctly routed to the integration or E2E rubric.** Under the integration rubric, `WebApplicationFactory<T>` / `HostBuilder` / `TestServer` setup is expected and positive (`dotnet.POS-3`). Under the E2E rubric, `StackFixture` / `IPlaywright` / browser-context factories / Testcontainers bringup are expected and positive. The `LC-7` carve-out in `extensions/dotnet-core.md` is the safety net for when routing is uncertain, not the primary mechanism.
- **Flagging traces, screenshots, or DOM snapshots captured on failure as `E-HC-F7`.** Diagnostics captured only on failure and never consumed by an assertion are positive (`E-POS-7`), not a smell. `E-HC-F7` targets snapshots *used as the assertion*, not snapshots *written as post-mortem artifacts*.
- **Treating a grep-based probable gap as a confirmed gap.** Step 2.5 gap findings are approximate. A public method tested indirectly via its caller, an interface method hit through its implementing class, and a private-by-intent-but-public-by-access method all look untested to grep. Mark each gap as probable in the report and recommend verification (mutation testing or manual review) before acting. Never convert a probable gap into a worklist item without a verification step.
- **Running SUT surface enumeration in quick mode.** Gap detection is suite-level. A quick-mode audit on a PR diff or single file does not have enough scope to make the enumeration meaningful. Skip step 2.5 entirely in quick mode.
- **Running mutation testing against an E2E target.** Mutation testing does not apply to E2E SUTs — the SUT is the whole deployed stack driven through a browser. Skip the step with state B or C and continue with static findings.
- **Flagging snapshot tests whose output *is* the published contract** — API responses with a documented schema, RFC test vectors, locale-compiled message catalogs. Snapshots of *unspecified* rendered output are the smell; snapshots of *specified* output are positive.
- **Treating `verify(mock...)` as a smell when the verified call *is* the observable behavior** — publishing to a message bus, emitting an audit event, calling an outbound HTTP endpoint. When the mocked thing is a process boundary and the verified call is what a client observes, it is specification.
- **Calling a test tautological because the assertion duplicates a simple case** — when the test is actually a boundary case (`sum([]) == 0`, `reverse("") == ""`, `max([x]) == x`).
- **Demanding intent for clearly-labeled characterization scaffolding** in legacy modules.
- **Flagging fixture-equals-output as tautology** when the transformation is identity by specification (idempotency, round-trip).
- **Scoring a quality verdict from coverage alone.** Coverage is a floor, not a goal.
- **Running the full deep-mode workflow when the user only asked for a single-file audit.**
- **Fabricating certainty** about whether an expected value is correct per a real spec. When the provenance is unknown, say `unknown` — do not guess.
- **Stacking findings** — reporting ten low-severity smells on one test instead of the one finding that actually matters.
- **Treating a high mutation score as a quality verdict.** A characterization test passes mutation testing just as well as a specification test — both kill mutants. Mutation score answers "do tests notice changes", not "are tests deriving from requirements". Never downgrade a static-audit verdict solely because mutation score is high; never upgrade a static-audit verdict solely because mutation score is high.
- **Running mutation testing in quick mode.** The mutation step is deep-mode only. A single-file or PR-diff audit does not trigger a mutation run, even when the tool is installed.
- **Running the mutation tool without the extension's detection command first.** Shelling into the tool to check its version has side effects and can be slow; use the extension's documented detection command (typically a tool-list query) so the audit skips gracefully on projects that don't have the tool installed.
- **Treating a mutation run failure as an audit failure.** Static findings stand on their own. If mutation testing fails for any reason — tool missing, SUT unsupported, run timeout, tool bug — fall through to state B or C in the Mutation testing subsection and continue.
