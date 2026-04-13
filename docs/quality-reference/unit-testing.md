# Unit Test Quality — Reference

A language- and framework-agnostic synthesis of authoritative guidance on unit test quality. Written to be directly usable as the rubric and reasoning substrate for a test-quality audit agent.

## Context

The failure mode this reference addresses: **tests are routinely written to validate whatever the code currently does, but current behavior can be wrong at the moment the test is written**. A test that "passes" in that state silently locks in the bug, and later refactors preserve it. The goal is to distinguish tests derived from *intent* from tests that are merely *echoes of the implementation*.

This document does not analyze any specific codebase. It states principles, smells, and rubrics.

---

## 1. The central problem: intent vs. echo

Two tests that look identical from the outside:

- **Specification test** — derived from a stated requirement. Could have been written *before* the implementation existed. Will fail unless the behavior matches intent.
- **Characterization test** — derived by running the code and recording its output. Could only have been written *after* the implementation. Freezes current behavior, including any bugs.

Michael Feathers coined "characterization test" for the legacy-refactor use case, and Martin Fowler endorses them as scaffolding — but both note they document what the code *does*, not what it *should* do. Unlabeled characterization tests are a smell, not a baseline.

**The operational question for every test:**

> Could this test have been written from the requirement alone, without having seen the implementation?

If no, the test is characterization. It may still be valuable, but it should be labeled, bounded, and replaced by intent-driven coverage over time.

---

## 2. Consensus characteristics of a quality unit test

Synthesized across Beck's Test Desiderata, Khorikov's Four Pillars, Osherove's Three Pillars, Fowler, Farley, Shore, Google Testing, and Microsoft Learn.

### 2.1 Behavioral and structure-insensitive
The test should fail when observable behavior changes and pass when only internal structure changes. This is the highest-leverage property — Khorikov calls "resistance to refactoring" the most violated pillar, and Beck's Test Desiderata split the requirement explicitly into *Behavioral* and *Structure-Insensitive*.

### 2.2 Derived from a stated requirement
The test name should read as a requirement sentence. Osherove's `MethodName_StateUnderTest_ExpectedBehavior` and Given/When/Then are both acceptable shapes. If the name describes *what the code does* instead of *what should happen for a user or caller*, the test is probably pinning current behavior.

### 2.3 Asserts observable outcomes through a public API
Khorikov's operational rule: "Unit tests should verify the observable behavior of the system under test, not the steps it takes to do that." Observable behavior is any operation or state that helps a client reach a goal; everything else is implementation detail. Google Testing Blog: "The class is an implementation detail, not a public API."

### 2.4 FIRST properties
Fast, Independent, Repeatable, Self-validating, Timely. Appears in virtually every source. Shared state between tests is the most common source of flake.

### 2.5 Specific on failure
Beck's *Specific*: when a test fails, the cause should be obvious. Multiple acts per test, assertion chains on unrelated facts, and broad end-to-end assertions all violate this.

### 2.6 Trustworthy, readable, maintainable
Osherove's Three Pillars. A test that isn't trusted gets ignored; an unreadable test gets deleted in the next refactor; an unmaintainable test gets disabled. All three erode the suite faster than no test at all.

### 2.7 Predictive
Beck: "if the tests all pass, the code under test should be suitable for production." Coverage and test count are not proxies for this — the only direct measures are mutation testing and production incident rate.

---

## 3. What to test — decision heuristics

| Heuristic | Applies to |
|---|---|
| Every branch of conditional logic needs at least one case | `if`, `switch`, ternary, early-return, pattern match |
| Happy path *and* each distinct sad path | Any function with failure modes |
| Boundary conditions: 0, 1, many, min, max, null, empty, overflow | Numeric, string, collection inputs |
| Invariants (things that must always be true) | Encryption non-determinism, serialization round-trips, ordering, idempotency |
| External contracts: request/response shape, status codes, headers | System boundaries |
| Observable side effects clients depend on | Persistence, outbound messages, emitted events |
| Business rules expressed in code | Pricing, permissions, eligibility, state transitions |

Deliberately **off** the list:

- Private methods — test through the public surface
- Getters/setters with no logic
- Framework behavior (ORMs, routers, serializers, DI containers) — trust the framework
- Pure delegation glue — test the thing it delegates to
- Third-party code you don't own

---

## 4. What not to test (or test lightly)

- **Implementation-detail classes.** Test the public API, not each collaborator.
- **The exact sequence of calls on a collaborator.** Prefer state verification over interaction verification.
- **Log message text**, unless the log is a published contract (audit events, structured telemetry with a schema).
- **Rendered output of a formatter/template** without a spec. A snapshot of whatever HTML you produced today is a characterization test by another name.
- **Private helpers** directly. If a private helper needs a test, it probably wants to be public on a different class.

---

## 5. Detectable signals

Split into high-confidence (clear smell) and low-confidence (worth flagging, requires context).

### 5.1 High-confidence smells

1. **No assertions.** The test body calls the SUT but asserts nothing — or the only assertion is that no exception was thrown.
2. **Tautological assertion.** The expected value is computed by duplicating the SUT's logic in the test body (`expected = input * 2; assertEqual(sut(input), input * 2)`). The test can't fail unless the SUT is unreachable.
3. **Pasted-literal assertion.** The expected value is an oddly specific literal (long string, big number, complex structure) with no comment, fixture reference, or spec citation tying it to a requirement. Likely pasted after running the code once.
4. **Logic in the test.** `if`, `for`, `while`, try/catch with a conditional assertion. Logic in tests means the test itself is unverified code.
5. **Mock-return-then-mock-called-with.** Test sets up a mock to return X, then asserts the mock was called with Y derived from X. No SUT behavior is verified — only that wiring is wired.
6. **Over-specified interaction assertions.** `verify(dep.method(a, b, c), times(3))` where the count and args reflect the current loop structure rather than an observable outcome.
7. **Names that describe HOW, not WHAT.** `Calls_Repository_Save_With_Entity` vs `Persists_Order_When_Checkout_Succeeds`. The former locks implementation; the latter states intent.
8. **Test depends on execution order.** Static mutable state, shared fixtures without reset, reliance on alphabetical ordering.
9. **Disabled or stubbed-out assertions.** Trivially-true assertions (`assert(true)`, `assertEquals(1, 1)`), commented-out assertions, skip directives with no linked issue.
10. **Snapshot / golden-file tests pinning unspecified output.** A snapshot assertion on rendered HTML, JSON, or any serialized form with no accompanying spec — pure characterization, regardless of which framework's snapshot helper is used.
11. **Mocks of the clock, filesystem, or network that return hardcoded "real" values.** Usually indicates the test was calibrated against a specific run.

### 5.2 Low-confidence smells (flag, ask for context)

1. **Heavy mocking of same-layer code.** Mocking classes in the same module as the SUT usually means the unit boundary is too small or the test is interaction-heavy.
2. **Assertions only on structural shape.** `expect(result).toHaveProperty('id')` without asserting what the id *should* be.
3. **Fixture passed through unchanged, then asserted equal to the fixture.** Might be a valid identity test, might be a tautology.
4. **One test per public method, all named `MethodName_Works`.** Suggests ceremony over thinking — a test per method instead of a test per behavior.
5. **Dates, GUIDs, or random values in the expected position.** Usually masks non-determinism.
6. **Zero negative tests** for a function with documented error modes.
7. **Excessive setup (>20 lines) for a single assertion.** Either the SUT has too many dependencies, or the test is reconstructing production state rather than isolating a behavior slice.
8. **Parameterized test where all cases assert the same thing.** The parameterization isn't doing work.

### 5.3 Positive signals (reward these)

1. **Test name reads as a requirement sentence.**
2. **Expected value has an external source:** a spec, RFC test vector, sample file, domain invariant, linked ticket.
3. **Assertions on the return value or published side effect**, not on internal mock invocations.
4. **Parameterized tests covering boundaries or equivalence classes**, with *varied* expected values.
5. **Separate tests for happy path and each distinct sad path.**
6. **Comments citing a requirement, spec, or invariant on non-obvious expected values.** Rare but gold.
7. **Tests that express an invariant** (round-trip, idempotency, commutativity) rather than a single point.

---

## 6. Detecting "current behavior" tests specifically

The hardest class to catch because they look identical to real tests. Signals, in decreasing strength:

1. **Provenance of the expected value.** If the literal can only have come from running the code, and there is no spec reference, it is probably characterization.
2. **Mutation testing survival.** A test that passes when the code it supposedly exercises is mutated is, by definition, insensitive to that code's behavior. Static analysis cannot run mutation testing but can recommend it as the ground truth.
3. **Name vocabulary.** Tests using the implementation's vocabulary (`Dictionary`, `Iterator`, `Handler`) instead of the domain's (`UserProfile`, `Order`, `Permission`) tend to be implementation-derived.
4. **Equality against a large literal blob.** Often a paste of observed output.
5. **Branch coverage without assertion differentiation.** Two tests cover different branches but assert the same thing → at least one is incidental coverage.
6. **Git provenance (if available).** Test added in the same commit as the implementation, with no red-green-refactor history.

---

## 7. The mock / test-double question

A recurring fault line in the literature. A quality audit should take a position rather than equivocate.

- **Fowler (Mocks Aren't Stubs):** mockist and classical schools are both valid, but mockist tests are more coupled to implementation and can "mask inherent errors" because mocks encode the author's possibly-wrong beliefs about a collaborator.
- **Khorikov:** mock only *inter-system* communications (things that leave the process). Do not mock *intra-system* communications.
- **Google (Increase Test Fidelity By Avoiding Mocks):** preference order is **real > fake > mock**. Mocks are a last resort because they only verify that code *calls* dependencies, not that it *behaves* correctly.
- **Shore (Nullable Infrastructure):** replace mocks entirely with real components that carry an embedded off-switch for I/O.

**Position:** treat mocks as a *cost*. Mocks are not automatically bad, but they carry a proof obligation: the mocked thing must represent an external boundary (HTTP, database, filesystem, clock, message bus, process boundary), not an owned collaborator. Mocking within the same module is a smell. Mocking at a process boundary is standard practice.

### 7.1 Fowler's test-double taxonomy

Fowler (following Meszaros) distinguishes five kinds of test double. The distinction matters for audit because different kinds carry different costs, and libraries like Moq / NSubstitute / FakeItEasy / Jest mocks / `unittest.mock` blur them into one construction syntax.

- **Dummy** — an object passed around but never actually used (e.g. `null` or a filler for an unused parameter). Free.
- **Stub** — provides canned answers to calls made during the test. The test verifies *state* (the SUT's return value or observable side effect), not which calls happened. Cost: the canned answers can drift from the real collaborator's contract, and there is no test-side signal when that drift happens.
- **Spy** — a stub that also records how it was called, for later inspection. Adds interaction coupling that a pure stub does not.
- **Mock** — pre-programmed with expectations about which calls it should receive; the test fails when the expectations are violated. Verifies *behavior* (the interaction between the SUT and the collaborator). Most expensive under refactoring — couples the test to the interaction, not the outcome.
- **Fake** — a working implementation with a shortcut unsuitable for production (e.g. an in-memory repository, a hash-map-backed cache, a `FakeTimeProvider`, a capture-style `TestLogger<T>`). No interaction coupling; the only cost is maintenance.

Google's preference order (**real > fake > mock**) and Khorikov's "mock only at process boundaries" rule both reduce to: *prefer the least-coupled double that still isolates the test*. A mock is a last resort; a fake is better when it exists; a real collaborator is best when the collaborator is owned.

**Audit implication.** When the rubric flags a finding like `HC-5` (mock-return-then-mock-called-with) or `HC-6` (over-specified interaction assertions), the finding only applies to test doubles being used as *mocks* (behavior verification) — not stubs (state verification) that happen to be constructed with the same library. Extensions must distinguish the two: `.Verify(...)` / `.Received(...)` / `MustHaveHappened(...)` → mock; only `.Setup(...)` / `.Returns(...)` → stub. See `extensions/dotnet-core.md` § *Test double classification* for the .NET-specific signals.

A `Fake*`, `InMemory*`, `TestLogger<T>`, `FakeTimeProvider`, or similarly-named working implementation is a **fake**, not a mock — even when constructed via a mocking library. These are positive signals (`dotnet.POS-5`, `dotnet.POS-6`), not smells.

---

## 8. Coverage, mutation, and the limits of static audit

**Signals static analysis can use:**

- **Line/branch coverage** — tells you what executed, *not* what was verified. Useless standalone. Useful only as a floor.
- **Mutation score** — the closest mechanical proxy for "are these assertions meaningful?" Petrovic & Ivankovic (Google, ICSE 2021) found surfaced mutants caused developers to strengthen tests in ~75% of cases.
- **Assertion density** — assertions per test. Too low (0–1 on a complex test) is a smell; too high (10+ on unrelated facts) is a different smell.
- **Test-to-code ratio** — informational only. High ratios can mean thorough testing *or* over-testing implementation.

**Things static analysis cannot know** (should admit, not fabricate):

- Whether the expected value is correct per the real domain spec.
- Mutation results — must be produced by an actual mutation run.
- Whether tests failed before the implementation was written — requires git + CI history.
- Whether the SUT's public API is the right boundary — that's a design judgment about the whole module.
- Flake rate and runtime — require execution history.

Be explicit about these limits. Ask for clarification rather than guess.

---

## 9. Per-test audit rubric

For each test case, a quality audit should produce:

1. **Intent statement.** In one sentence, what requirement does this test encode? If unarticulable, that itself is a finding.
2. **Expected-value provenance.** One of: spec / fixture / RFC or standard / domain invariant / pasted literal / unknown.
3. **Assertion target.** Return value / published side effect / internal mock invocation / structural shape only / none.
4. **Coupling signals.** Structure-sensitive elements detected (with locations).
5. **Smells matched** from §5.1 and §5.2.
6. **Positive signals matched** from §5.3.
7. **Behavior-vs-echo verdict.** Specification / characterization / ambiguous.
8. **Severity.** Block / warn / info.
9. **Recommended action.** Rewrite from requirement / add missing assertion / split / delete / keep.

The rubric is deliberately opinionated. Soft audits produce ignorable noise.

---

## 10. Directive principles

Distilled from the consensus in §2–§7, written as directives an audit agent can apply directly.

1. **Assume the code under test may be wrong.** Do not use it as ground truth for the expected value of any assertion.
2. **A test without a stateable requirement is a characterization test.** Treat it as such, regardless of how it looks.
3. **Prefer state over interaction.** Mock-invocation assertions are a last resort and carry a proof obligation.
4. **Test through the public API.** Private methods and implementation-detail classes are not valid test subjects.
5. **Mock only at process boundaries.** Mocking owned collaborators is a smell.
6. **Names encode requirements.** A test name describing *how* the code works is a red flag.
7. **Coverage is a floor, not a goal.** Mutation testing is the ceiling-quality signal; recommend it when the suite appears high-coverage but shallow.
8. **Be honest about what cannot be determined from source alone.** Ask for commit history, spec links, or a mutation run rather than inventing certainty.
9. **One finding per test is better than ten.** Prioritize the highest-severity smell; don't stack.
10. **Reward positive signals explicitly.** An audit that only complains gets tuned out.

---

## Sources

### Microsoft Learn
- [Unit testing best practices for .NET](https://learn.microsoft.com/dotnet/core/testing/unit-testing-best-practices)
- [Mutation testing](https://learn.microsoft.com/dotnet/core/testing/mutation-testing)
- [Use code coverage for unit testing](https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage)
- [Test ASP.NET Core MVC apps](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/test-asp-net-core-mvc-apps)

### Martin Fowler
- [UnitTest](https://martinfowler.com/bliki/UnitTest.html)
- [Mocks Aren't Stubs](https://martinfowler.com/articles/mocksArentStubs.html)
- [Testing resource index](https://martinfowler.com/testing/)

### Kent Beck
- [Test Desiderata](https://testdesiderata.com/)
- [Coupling (Tidy First?)](https://tidyfirst.substack.com/p/coupling)

### Vladimir Khorikov
- [Unit Testing: Principles, Practices, and Patterns — chapter 1 excerpt](https://enterprisecraftsmanship.com/files/Unit-Testing-Chapter-1-Excerpt.pdf)
- [Don't mock your database](https://vkhorikov.medium.com/dont-mock-your-database-it-s-an-implementation-detail-8f1b527c78be)

### Dave Farley
- [Test *Driven* Development](https://www.davefarley.net/?p=220)

### Roy Osherove
- [The Art of Unit Testing](https://www.artofunittesting.com/)
- [Naming standards for unit tests](https://osherove.com/blog/2005/4/3/naming-standards-for-unit-tests.html)

### James Shore
- [Testing Without Mocks: A Pattern Language](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks)

### Google Testing Blog
- [Don't Overuse Mocks](https://testing.googleblog.com/2013/05/testing-on-toilet-dont-overuse-mocks.html)
- [Prefer Testing Public APIs Over Implementation-Detail Classes](https://testing.googleblog.com/2015/01/testing-on-toilet-prefer-testing-public.html)
- [Increase Test Fidelity By Avoiding Mocks](https://testing.googleblog.com/2024/02/increase-test-fidelity-by-avoiding-mocks.html)

### Mutation testing
- [Stryker Mutator](https://stryker-mutator.io/)
- [PIT (Java)](https://pitest.org/)
- Petrovic & Ivankovic, *Does Mutation Testing Improve Testing Practices?*, ICSE 2021

### Legacy / characterization tests
- Michael Feathers, *Working Effectively with Legacy Code* — origin of "characterization test"
