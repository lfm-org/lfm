# Integration Test Quality — Reference

A language- and framework-agnostic synthesis of authoritative guidance on integration test quality. Sibling to [unit-testing.md](unit-testing.md). Written to be directly usable as the rubric and reasoning substrate for a test-quality audit agent.

## Context

Integration tests have a different failure mode from unit tests. Unit tests fail by pinning current behavior instead of stated intent ("intent vs. echo" — see [unit-testing.md §1](unit-testing.md)). Integration tests fail by being **unable to answer what they integrate, and why a unit test couldn't prove the same thing**. When that question has no answer, the test either duplicates a unit test at higher cost or sprawls into an unfocused whole-system test. Fidelity gaps and flake (§6, §7) follow from this confusion — they are downstream consequences, not independent problems.

This document covers integration tests in two sub-lanes that share principles but have distinct smells:

- **Sub-lane A — in-process integration.** Real modules wired together in one process, with real adjacent dependencies (DB, queue, filesystem) where practical. The classic "middle tier" between unit and end-to-end.
- **Sub-lane B — out-of-process contract.** The system under test is a *deployed artifact*, exercised as a black box through its public protocol (HTTP, message bus, gRPC). The unit boundary is the deployment unit, not the class.

Browser-driven full-stack tests are explicitly out of scope; they belong in the end-to-end lane and warrant their own reference.

This document does not analyze any specific codebase. It states principles, smells, and rubrics.

---

## 1. The central problem: scope and the "couldn't-a-unit-test" test

The operational question for every integration test:

> What is this test integrating, and why couldn't a unit test prove the same thing?

If the answer is *nothing new*, the test is a slower, flakier duplicate of a unit test and should be deleted or moved.
If the answer is *everything at once*, the test is unfocused, hard to diagnose, and slow — it has no defensible scope and every smell in §5 will land on it.
If the answer is *the seam between A and B works against real B*, the test has a clear job and the rest of this document tells you whether it does that job well.

A test labeled "integration" without a stateable scope is a smell regardless of how green it runs. Integration is not a vibe. It is a claim about which seam is being exercised against which real dependency.

### Vocabulary used throughout

- **Hermetic** (Google): the test brings every piece of state it needs with it, depends on nothing external, and produces the same result on every run. Hermeticity is a property of the test environment, not just the assertions.
- **Test sizes** (Google): *small* = single process, no I/O, no network; *medium* = single machine, localhost only, no external network; *large* = multi-machine. Sub-lane A is almost always *medium*. Sub-lane B varies; tests against an artifact running in the same container suite are medium, tests against a deployed environment are large.
- **Narrow vs. broad** (Fowler): a *narrow* integration test exercises the SUT against exactly one external dependency. A *broad* integration test sweeps in several at once. Narrow is the default; broad is a deliberate exception when the seams genuinely interact.
- **Process boundary vs. deployment boundary**: Khorikov's rule for unit tests is "mock only across process boundaries." For integration tests in sub-lane B the relevant boundary is the *deployment unit* — anything outside the artifact-under-test is mockable; anything inside is not.

---

## 2. Consensus characteristics of a quality integration test

Synthesized across Fowler, Khorikov, Shore, Beck, the Google testing book, and Microsoft Learn.

### 2.1 Defensible scope
The test names the seam it exercises and the reason that seam needs a real dependency. "It's an integration test" is not a scope. "It verifies that the order repository's `FindByCustomer` query returns rows in the order the database returns them under a partition-key scan" is.

### 2.2 Hermetic
Every piece of state the test reads, it either created or set up explicitly. No reliance on data left by a previous test, a previous run, an external service, or wall-clock time. A hermetic test can run twice in a row, in parallel with itself, and offline, and still produce the same result.

### 2.3 Isolated from other tests
No shared mutable fixture. No assumption about ordering. Cleanup runs on failure as well as on success. Two tests touching the same table own disjoint rows or scope themselves to disjoint partitions/keys.

### 2.4 Asserts observable behavior through a real seam
The seam being exercised is a real dependency, not a fake. The assertion is on the seam's observable behavior — the row that landed, the response that came back, the message that was published — not on whether some collaborator was called.

### 2.5 Diagnosable on failure
A single named seam, a single named scope, a single named expected outcome. When it fails the failure points at one thing. Broad integration tests (§3) violate this by construction and must justify it.

### 2.6 Deterministic
No `Sleep`, no `WaitFor` with a wall-clock budget, no retry-until-green loops. Time is controlled. Randomness is seeded. Network is in-band (a real dependency, started by the test) or stubbed at the deployment boundary, never both.

### 2.7 Fast enough to run in CI on every commit
Beck's *SlowTest* applies double here. A test that takes two minutes to spin up an environment will be skipped, and a skipped test catches nothing. Ship setup in the test, not around it; reuse environments where it does not compromise hermeticity; quarantine the slow ones into a separate lane and accept that they run less often.

### 2.8 Trustworthy, readable, maintainable
Osherove's three pillars (see [unit-testing.md §2.6](unit-testing.md)). An integration test that is not trusted gets disabled within a quarter; an integration test that nobody can read gets deleted in the next refactor.

---

## 3. What to test — decision heuristics

### 3.1 Shared: when an integration test earns its place

| Heuristic | Applies to |
|---|---|
| There is a real seam against a real adjacent system that unit tests cannot prove cheaply | Wiring, DI composition, framework integration |
| The behavior depends on the real dependency's semantics, not just its interface | DB query ordering, transaction visibility, blob storage consistency |
| The behavior crosses a serialization boundary | Request/response shape, message envelope, schema round-trips |
| The behavior is the seam itself | Migrations, retry/idempotency, header propagation |

### 3.2 Sub-lane A — in-process

| Heuristic | Notes |
|---|---|
| DI composition / wiring correctness | The container actually resolves the SUT against real implementations |
| Real-DB query semantics | Query ordering, partition behavior, index usage, paging |
| Schema / migration behavior | See §3.4 below |
| Serialization round-trips | JSON, Protobuf, Avro — the shape the SUT actually emits and parses |
| Framework integration | Routing, middleware, filters, model binding, auth pipeline |
| Background workers against a real queue | Visibility timeout, dead-letter, retry counts |

### 3.3 Sub-lane B — out-of-process contract

| Heuristic | Notes |
|---|---|
| Contract shape | Status codes, error envelope, header presence, content type |
| Auth boundary | Anonymous, valid token, expired token, insufficient scope |
| Idempotency and retry | The SUT's actual retry path, not a mocked one |
| Header propagation | Correlation ids, trace context, tenant headers |
| Pagination and ordering contract | What the consumer actually depends on |
| Deployment-shaped bugs | Misconfigured connection strings, missing env vars, container health checks |

### 3.4 Schema and migration tests — a distinct category

Migrations have their own rules because their failure modes are temporal:

- A migration test must run against a **populated** state representative of the version it migrates from. Empty-DB migration tests prove only that the migration parses.
- A migration test must assert on the **post-state**: row counts, distinct values, presence of new columns and indexes, absence of removed-with-cause columns.
- If the migration policy is expand-only, the test must also assert that the *previous* shape still works after the migration runs (the contract that old code can keep reading the migrated database).
- If the migration policy permits rollback, the rollback path must be tested. If it does not, the test must say so.

### 3.5 Narrow vs. broad

Default to **narrow**. One SUT, one real dependency, one seam. Narrow integration tests are diagnosable and stable.

Use **broad** only when the seams interact in a way unit tests and narrow integration tests genuinely cannot reach — for example, a saga that writes to a DB *and* publishes a message *and* the contract is "the publish only happens if the write committed." Broad tests must justify their breadth in their name or a comment.

### 3.6 Consumer-driven contracts

At a service-to-service boundary, prefer a consumer-driven contract test (Pact-style) over an in-process integration test that stands up the downstream service. Pact's thesis: bilateral integration tests between services do not scale — you cannot run every consumer's integration suite against every provider on every commit. Consumer-driven contracts flip the direction: each consumer records its expectations as a pact, and the provider verifies against the union of pacts. A passing contract test subsumes the corresponding mock; if there is a contract, the mock is redundant.

---

## 4. What not to test (or test lightly)

- **Things unit tests already prove cheaper.** Pure logic, validation, pricing rules, formatting. Moving them to the integration lane buys nothing but flake.
- **Things end-to-end owns.** Full user journeys with a browser. If a test needs the UI, it is not an integration test under this rubric.
- **Third-party infrastructure.** Cloud providers, managed databases, message buses *as products*. Trust the platform; test your usage of it.
- **Framework internals.** Routing, DI containers, ORMs, serializers — at the framework's level. Test your configuration of them, not the framework itself.
- **Unstructured logs and traces.** Assertable *only* when they are a published contract with a schema (audit events, structured telemetry consumed by an SLO). Asserting on a free-text log message couples the test to a string the next refactor will rewrite.
- **Rendered HTML or formatter output without a spec.** A snapshot of the response body produced today is a characterization test by another name (see [unit-testing.md §5.1.10](unit-testing.md)) — amplified at the integration layer because the underlying data drifts faster than the code does.

---

## 5. Detectable signals

Codes are prefixed `I-` to distinguish from the unit-testing rubric, which uses bare `HC-` / `LC-`.

### 5.1 High-confidence smells, sub-lane A (in-process) — `I-HC-A1..I-HC-A10`

1. **`I-HC-A1`** — Every dependency mocked. No real seam exercised. The test claims to integrate but integrates nothing; belongs in the unit lane.
2. **`I-HC-A2`** — Shared seed data mutated across tests. Cross-test pollution by construction.
3. **`I-HC-A3`** — Test depends on migration ordering without declaring it. Reorder the file list, watch the suite collapse.
4. **`I-HC-A4`** — Shared container with no per-test data scoping. The first test's writes are the second test's preconditions.
5. **`I-HC-A5`** — Wall-clock sleeps, polling budgets, retry-until-green loops, any `wait-for` primitive whose exit condition is the elapsed clock rather than a state predicate. Flake in slow motion.
6. **`I-HC-A6`** — Test asserts on log text that is not a published audit event. Couples the test to a string.
7. **`I-HC-A7`** — Migration test runs against an empty database and asserts nothing about row state. Proves the migration parses; nothing more.
8. **`I-HC-A8`** — Snapshot of a full entity graph with no schema source. Pure characterization at the integration layer.
9. **`I-HC-A9`** — Test writes data and never cleans up — or only cleans up on the happy path. Flake compounds with run count.
10. **`I-HC-A10`** *(integration analogue of unit-testing §6)* — Incidental coverage. The test sweeps a large amount of code into execution but the only assertion is "no exception thrown." High line coverage, near-zero behavioral coverage.
11. **`I-HC-A11`** — Fragile setup. The arrange block depends on state or behavior it cannot guarantee: its own retry loop or wait-for-state, seed data the test did not create, a live third-party boundary with no stub, or a shared factory that mutates across tests without per-test isolation. Distinct from `I-HC-A2`/`I-HC-A4` (shared-state *mutation*): this smell targets the *arrange block itself* as a risk surface — setup that is non-deterministic by construction. Rewrite: seed preconditions with per-test factories and unique keys (`I-POS-4`); stub third-party boundaries at the process boundary; if the setup genuinely needs retries, that is a finding against the backend's determinism, not a reason to retry in the test.

### 5.2 High-confidence smells, sub-lane B (out-of-process contract) — `I-HC-B1..I-HC-B8`

1. **`I-HC-B1`** — Assertion on an implementation-detail response field (internal id format, debug field, pagination cursor encoding) with no spec reference. The next refactor breaks the test, not the contract.
2. **`I-HC-B2`** — Test hits a test-only endpoint that does not exist in production. Whatever the test proves, it does not prove what production does.
3. **`I-HC-B3`** — Snapshot of a full response body with no OpenAPI / JSON Schema / Protobuf source. Drifts on every product change unrelated to the contract.
4. **`I-HC-B4`** — Hardcoded port, container name, hostname, or environment URL. The test runs in exactly one environment by accident.
5. **`I-HC-B5`** — The downstream service is mocked at the transport layer. Defeats the entire sub-lane: the test exercises no real seam. Either move it to the unit lane or replace it with a contract test.
6. **`I-HC-B6`** — Retry test that stubs the transport. The SUT's retry code path is never really executed; only the wiring around it is.
7. **`I-HC-B7`** — Auth test with only a happy-path valid token. No expired token, no missing token, no insufficient scope, no malformed token. The negative space is exactly where auth bugs live.
8. **`I-HC-B8`** — Contract test whose "expected" payload was pasted from a recorded run, with no consumer behind it. The contract is fictional.

### 5.3 Low-confidence smells, shared across sub-lanes — `I-LC-1..I-LC-6`

1. **`I-LC-1`** — One giant test class covering unrelated features. Suggests no per-feature thinking; more likely to harbor cross-test coupling.
2. **`I-LC-2`** — Test name ends in `_Works` or `_Integration` or `_EndToEnd` with no requirement in the name.
3. **`I-LC-3`** — Expected values with no external provenance. May be fine; may be a paste from the SUT.
4. **`I-LC-4`** — Parameterized test where every case asserts the same thing. The parameterization is doing no work.
5. **`I-LC-5`** — Fixture rebuilt from scratch in every test. May be correct hygiene; may indicate the fixture is too ambitious and the test is reconstructing production state instead of isolating a slice.
6. **`I-LC-6`** — Broad integration test where a narrow one would do. Seam conflation.

### 5.4 Positive signals (reward these)

1. **Test name reads as a requirement sentence and names the seam.** `Persists_Order_To_Database_When_Checkout_Succeeds` beats `OrderRepositoryIntegrationTest`.
2. **Expected value has external provenance.** Spec, OpenAPI, JSON Schema, RFC, consumer pact, ticket, domain invariant.
3. **Narrow by default.** One seam per test. Broad tests carry a comment explaining why.
4. **Per-test data ownership.** Factory + cleanup. No shared mutable fixture.
5. **Hermetic by construction.** Test runs offline, runs in parallel with itself, produces the same result twice in a row.
6. **Asserts on a published contract** with a cited source — status code, error envelope, audit event schema, OpenAPI fragment.
7. **Tests that express an invariant** — round-trip, idempotency, "publish only after commit" — rather than a single point.

---

## 6. The fidelity / test-double question — position

A recurring fault line. The audit takes a position rather than equivocate.

- **Inside an integration test, a mock is almost always a scope leak.** If the seam you care about is faked, the test belongs in the unit lane and should be relabeled. The proof obligation: every mock in an integration test must point at a process or deployment boundary *and* a reason a contract test cannot cover it.
- **Sub-lane A.** Use real adjacent dependencies. Real DB in a container, real filesystem, real in-memory bus, real HTTP server. Mock only outside the deployment unit — third-party APIs you do not own, payment processors, identity providers in destructive flows.
- **Sub-lane B.** Replace mocks of downstream services with consumer-driven contracts where feasible. If a contract exists, the mock is redundant; if a contract does not exist, the mock encodes the author's possibly-wrong beliefs about a collaborator (Fowler's classic objection to mockist tests, amplified at the service boundary).
- **Shore's nullable infrastructure** offers a third option: real components carrying an embedded off-switch for I/O. Where the platform supports it, this collapses the mock-vs-real choice into a single artifact and is preferred over either pole.

---

## 7. Flake, isolation, and environment — the second-biggest failure mode

Flake is the second-biggest failure mode after scope confusion, and the two are linked: a test with a defensible narrow scope rarely flakes, and a test that flakes is often a broad test in disguise. Quarantining flakes is not a fix; it is a signal to re-examine scope.

### 7.1 Sources of flake

- **Shared state.** Two tests reading or writing the same row, key, blob, or topic.
- **Container lifetime.** A container reused across tests without resetting the state the test depends on.
- **Time.** Reads of the wall clock, wall-clock sleeps, retry budgets measured in seconds rather than in state transitions.
- **Randomness.** Unseeded RNGs, GUIDs in assertion positions.
- **Network.** External HTTP calls inside a test that claims to be hermetic.
- **Test ordering.** Implicit or explicit assumptions about which test ran first.

### 7.2 Rules

- Each test owns its data. Per-test factories generate per-test keys. No two tests in the suite share a row by accident.
- Cleanup runs on failure as well as success. The teardown path must be the same path the assertion path takes when it fails.
- No test asserts wall-clock behavior without a controlled clock. Inject the clock at the seam.
- Containers are reset between runs, or tests scope their data per-run with a unique prefix.
- No test reads state another test wrote. If two tests need the same precondition, both must build it themselves.

### 7.3 Test data strategy

Per-test factories beat shared seed data. Shared seed data starts as a convenience and ends as the suite's biggest source of incidents — every new test either inherits assumptions from the seed or invalidates it for its neighbors. Pay the per-test setup cost. The cost compounds linearly; the seed-data debt compounds quadratically with test count.

---

## 8. Coverage, trace assertions, and the limits of static audit

### 8.1 Signals static analysis can use

- **Missing assertions** — a test that calls the SUT and asserts nothing observable is detectable without executing the test.
- **Sleeps and polling loops** — any wall-clock sleep, fixed-budget wait, or retry-until-green loop — all detectable as patterns.
- **Snapshot usage** — `MatchesSnapshot`, `Verify`, golden file diffs — high-confidence when the snapshot has no schema source.
- **Same-module mocking** — mocks of types declared in the same project as the SUT signal scope confusion.
- **Cross-test data leakage patterns** — shared fixtures, missing cleanup, hardcoded keys, fixture mutation.
- **Hardcoded ports and hostnames** — detectable lexically.

### 8.2 Things static analysis cannot know (admit, do not fabricate)

- **Flake rate.** Requires execution history.
- **Whether the seam is the right one.** Requires a design judgment about the system.
- **Whether the test catches anything a unit test could not.** Requires mutation testing on the SUT path or historical failure attribution.
- **Real environmental coupling.** Requires running the test in an environment it was not designed for and observing what breaks.
- **Whether the contract test reflects an actual consumer.** Requires knowing the consumer.

### 8.3 Recommended runtime signals

- **Mutation testing** on the SUT path covered by the integration test. The closest mechanical proxy for "does this test catch anything." See [unit-testing.md §8](unit-testing.md).
- **Flake quarantine rate.** A growing quarantine list is the strongest signal that scope is wrong somewhere.
- **Historical failure attribution.** What did the test catch in the last N weeks? If the answer is "nothing," the test is a candidate for deletion.
- **Contract drift alerts** from CDC tooling — Pact Broker, Spring Cloud Contract.

Be explicit about the limits. Ask for runtime signals rather than guess.

---

## 9. Per-test audit rubric

For each integration test case, an audit should produce:

1. **Scope statement.** In one sentence: which seam is exercised, and why an integration test (not a unit test).
2. **Sub-lane.** A (in-process) or B (out-of-process contract).
3. **Test size** (Google sizing). Small / medium / large.
4. **Seam narrowness** (Fowler). Narrow / broad. Broad tests must carry a justification.
5. **Fixture and data provenance.** Per-test factory / shared seed / external snapshot / pasted literal / unknown.
6. **Assertion target.** Published contract / observable state / response value / internal mock invocation / structural shape only / none.
7. **Smells matched** from §5.1, §5.2, §5.3 (cite codes: `I-HC-A4`, `I-LC-2`, etc.).
8. **Positive signals matched** from §5.4.
9. **Behavior-vs-incidental verdict.** Specification / incidental / ambiguous.
10. **Severity.** Block / warn / info.
11. **Recommended action.** Rewrite from requirement / add assertion / split / narrow the seam / move to unit lane / replace with contract test / delete / keep.

The rubric is deliberately opinionated. Soft audits produce ignorable noise.

---

## 10. Directive principles

Distilled from §2–§7, written as directives an audit agent can apply directly.

1. **Every integration test must state what it integrates and why a unit test wouldn't do.** A test without a stateable scope is a smell regardless of how green it runs.
2. **Prefer narrow over broad.** One seam per test. Broad tests must justify their breadth.
3. **Inside an integration test, a mock is a scope leak.** Justify it (process or deployment boundary, no contract available) or move the test.
4. **At a service boundary, prefer a consumer-driven contract over a mock.** A contract subsumes the mock; the mock encodes the author's beliefs.
5. **Hermetic by construction.** A test that depends on state it did not create depends on luck.
6. **Test data is owned per-test.** Shared mutable fixtures are a smell.
7. **Log and trace assertions are valid only against published contracts.** Free-text logs are not contracts.
8. **Snapshot equals characterization, amplified.** Snapshot tests at the integration layer drift faster than the code does.
9. **Flake is a scope signal, not a quarantine problem.** Quarantining flakes hides the underlying scope confusion.
10. **Coverage is a floor.** Mutation testing on the SUT path and historical failure attribution are the ceiling.
11. **Be honest about what cannot be determined from source alone.** Ask for runtime signals, mutation runs, contract pacts, or environment history rather than inventing certainty.

---

## 11. Sources

### Martin Fowler
- [IntegrationTest](https://martinfowler.com/bliki/IntegrationTest.html)
- [NarrowIntegrationTest](https://martinfowler.com/bliki/IntegrationTest.html) (same page; defines narrow vs. broad)
- [ContractTest](https://martinfowler.com/bliki/ContractTest.html)
- [SelfInitializingFake](https://martinfowler.com/bliki/SelfInitializingFake.html)
- [Mocks Aren't Stubs](https://martinfowler.com/articles/mocksArentStubs.html)
- [Testing resource index](https://martinfowler.com/testing/)

### Pact and consumer-driven contracts
- [Pact: How Pact works](https://docs.pact.io/)
- [Pact: Consumer-driven contracts](https://docs.pact.io/getting_started/what_is_pact)

### Google Testing
- Winters, Manshreck, Wright — *Software Engineering at Google*, ch. 11 ("Testing Overview"), for the small / medium / large test sizing taxonomy and the hermetic-server pattern.
- [Just Say No to More End-to-End Tests](https://testing.googleblog.com/2015/04/just-say-no-to-more-end-to-end-tests.html)
- [Hermetic Servers](https://testing.googleblog.com/2012/10/hermetic-servers.html)

### Vladimir Khorikov
- [Unit Testing: Principles, Practices, and Patterns — chapter 1 excerpt](https://enterprisecraftsmanship.com/files/Unit-Testing-Chapter-1-Excerpt.pdf) — process-boundary rule, extended here to deployment-unit boundaries for sub-lane B.
- [Don't mock your database](https://vkhorikov.medium.com/dont-mock-your-database-it-s-an-implementation-detail-8f1b527c78be)

### James Shore
- [Testing Without Mocks: A Pattern Language](https://www.jamesshore.com/v2/projects/nullables/testing-without-mocks) — nullable infrastructure as an alternative to both mocks and shared environments.

### Kent Beck
- [Test Desiderata](https://testdesiderata.com/) — characteristics that apply equally to integration tests; *SlowTest* applies double.

### Microsoft Learn
- [Integration tests in ASP.NET Core](https://learn.microsoft.com/aspnet/core/test/integration-tests) — a representative in-process integration pattern for sub-lane A.
- [Test ASP.NET Core MVC apps](https://learn.microsoft.com/dotnet/architecture/modern-web-apps-azure/test-asp-net-core-mvc-apps)

### Testcontainers
- [Testcontainers documentation](https://testcontainers.com/) — hermetic real dependencies in sub-lane A.

### Sibling reference
- [unit-testing.md](unit-testing.md) — the unit-test rubric this document composes with. Where the integration rubric is silent on a topic that applies to both lanes (assertion specificity, naming, FIRST properties, the limits of coverage), defer to the unit-testing reference.
