# Smell Catalog (core, framework-neutral)

Compact reference for cite-by-code usage in audit reports. Codes align 1:1 with [../../../docs/quality-reference/unit-testing.md §5](../../../docs/quality-reference/unit-testing.md) (unit rubric) and [../../../docs/quality-reference/integration-testing.md §5](../../../docs/quality-reference/integration-testing.md) (integration rubric). The reference docs are the source of truth — this file exists so audit findings can cite codes like `HC-2`, `LC-3`, `I-HC-A4`, `I-LC-2` instead of restating each smell.

Framework-specific smells are namespaced in their extension file (e.g. `dotnet.HC-1`). Each extension smell may declare an `Applies to:` field of `unit`, `integration`, or `unit, integration`; absent tag defaults to `unit` for backwards compatibility.

## Unit rubric

Applies when `SKILL.md` step 0b selects the unit rubric. Cite as `HC-N`, `LC-N`, `POS-N`.

### High-confidence smells (HC-*)

`HC-1` — No assertions; body calls SUT but asserts nothing (or only "no exception thrown").
`HC-2` — Tautological assertion; expected value is computed by duplicating SUT logic in the test.
`HC-3` — Pasted-literal assertion; oddly specific expected value with no spec, fixture, or comment provenance.
`HC-4` — Logic in the test; `if`, `for`, `while`, try/catch with conditional assertion.
`HC-5` — Mock-return-then-mock-called-with; test sets up a mock to return X then asserts the mock was called with Y derived from X.
`HC-6` — Over-specified interaction assertions; `verify(..., times(N))` where count/args reflect loop structure, not observable outcome.
`HC-7` — Name describes HOW, not WHAT; e.g. `Calls_Repository_Save_With_Entity` vs `Persists_Order_When_Checkout_Succeeds`.
`HC-8` — Test depends on execution order; static mutable state, shared fixtures without reset, alphabetical ordering reliance.
`HC-9` — Disabled or stubbed-out assertions; `assertTrue(true)`, commented-out asserts, `skip()` with no linked issue.
`HC-10` — Snapshot tests pinning unspecified output; `toMatchSnapshot()` on rendered output with no accompanying spec.
`HC-11` — Mocks of clock/fs/network returning hardcoded "real" values; signals the test was calibrated against a specific run.

### Low-confidence smells (LC-*)

`LC-1` — Heavy mocking of same-layer code; mocking classes owned by the same module as the SUT.
`LC-2` — Assertions only on structural shape; `hasProperty('id')` without asserting what the id should be.
`LC-3` — Fixture passed through unchanged and asserted equal to the fixture; possible valid identity, possible tautology.
`LC-4` — One test per public method, all named `MethodName_Works`; ceremony over behavior thinking.
`LC-5` — Dates, GUIDs, or random values in the expected position; usually masks non-determinism.
`LC-6` — Zero negative tests for a function with documented error modes.
`LC-7` — Excessive setup (>20 lines) for a single assertion; either too-many-dependencies SUT or test reconstructs production state instead of isolating behavior.
`LC-8` — Parameterized test where all cases assert the same thing; parameterization isn't doing work.

### Positive signals (POS-*)

`POS-1` — Test name reads as a requirement sentence.
`POS-2` — Expected value has an external source (spec, RFC vector, sample file, domain invariant, linked ticket).
`POS-3` — Assertions on return value or published side effect (not on internal mock invocations).
`POS-4` — Parameterized tests covering boundaries/equivalence classes with *varied* expected values.
`POS-5` — Separate tests for happy path and each distinct sad path.
`POS-6` — Comments citing a requirement, spec, or invariant on non-obvious expected values.
`POS-7` — Test expresses an invariant (round-trip, idempotency, commutativity, associativity, bounds).

## Integration rubric

Applies when `SKILL.md` step 0b selects the integration rubric. Codes are prefixed `I-` to distinguish from unit-rubric codes above. Sub-lane A is in-process; sub-lane B is out-of-process contract. See [../../../docs/quality-reference/integration-testing.md §5](../../../docs/quality-reference/integration-testing.md) for the full rationale.

### High-confidence smells, sub-lane A / in-process (I-HC-A*)

`I-HC-A1` — Every dependency mocked; no real seam exercised. Belongs in the unit lane.
`I-HC-A2` — Shared seed data mutated across tests; cross-test pollution by construction.
`I-HC-A3` — Test depends on migration ordering without declaring it.
`I-HC-A4` — Shared container with no per-test data scoping; first test's writes are the second test's preconditions.
`I-HC-A5` — `Thread.Sleep`, `WaitFor`, retry-until-green loops; flake in slow motion.
`I-HC-A6` — Test asserts on log text that is not a published audit event.
`I-HC-A7` — Migration test runs against an empty database and asserts nothing about row state.
`I-HC-A8` — Snapshot of a full entity graph with no schema source; characterization at the integration layer.
`I-HC-A9` — Test writes data and never cleans up, or only cleans up on the happy path.
`I-HC-A10` — Incidental coverage; test sweeps a large amount of code into execution but the only assertion is "no exception thrown."

### High-confidence smells, sub-lane B / out-of-process contract (I-HC-B*)

`I-HC-B1` — Assertion on an implementation-detail response field with no spec reference.
`I-HC-B2` — Test hits a test-only endpoint that does not exist in production.
`I-HC-B3` — Snapshot of a full response body with no OpenAPI / JSON Schema / Protobuf source.
`I-HC-B4` — Hardcoded port, container name, hostname, or environment URL.
`I-HC-B5` — Downstream service mocked at the transport layer; defeats the sub-lane, belongs in unit lane or as a contract test.
`I-HC-B6` — Retry test stubs the transport; the SUT's retry code path is never really executed.
`I-HC-B7` — Auth test with only a happy-path valid token; no negative cases.
`I-HC-B8` — Contract test whose "expected" payload was pasted from a recorded run with no consumer behind it.

### Low-confidence smells, shared across sub-lanes (I-LC-*)

`I-LC-1` — One giant test class covering unrelated features.
`I-LC-2` — Test name ends in `_Works` / `_Integration` / `_EndToEnd` with no requirement in the name.
`I-LC-3` — Expected values with no external provenance; may be fine, may be pasted from the SUT.
`I-LC-4` — Parameterized test where every case asserts the same thing.
`I-LC-5` — Fixture rebuilt from scratch in every test; may indicate the fixture is too ambitious.
`I-LC-6` — Broad integration test where a narrow one would do; seam conflation.

### Positive signals (I-POS-*)

`I-POS-1` — Test name reads as a requirement sentence and names the seam being exercised.
`I-POS-2` — Expected value has external provenance (spec, OpenAPI, JSON Schema, RFC, consumer pact, ticket, domain invariant).
`I-POS-3` — Narrow by default; one seam per test. Broad tests carry a comment explaining why.
`I-POS-4` — Per-test data ownership; factory plus cleanup, no shared mutable fixture.
`I-POS-5` — Hermetic by construction; runs offline, runs in parallel with itself, produces the same result twice in a row.
`I-POS-6` — Asserts on a published contract (status code, error envelope shape, audit event schema, OpenAPI fragment) with a cited source.
`I-POS-7` — Test expresses an invariant (round-trip, idempotency, "publish only after commit") rather than a single point.
