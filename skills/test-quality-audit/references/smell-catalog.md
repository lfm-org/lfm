# Smell Catalog (core, framework-neutral)

Compact reference for cite-by-code usage in audit reports. Codes align 1:1 with [../../../docs/quality-reference/unit-testing.md §5](../../../docs/quality-reference/unit-testing.md). The reference doc is the source of truth — this file exists so audit findings can say "HC-2, LC-3" instead of restating each smell.

Framework-specific smells are namespaced in their extension file (e.g. `dotnet.HC-1`).

## High-confidence smells (HC-*)

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

## Low-confidence smells (LC-*)

`LC-1` — Heavy mocking of same-layer code; mocking classes owned by the same module as the SUT.
`LC-2` — Assertions only on structural shape; `hasProperty('id')` without asserting what the id should be.
`LC-3` — Fixture passed through unchanged and asserted equal to the fixture; possible valid identity, possible tautology.
`LC-4` — One test per public method, all named `MethodName_Works`; ceremony over behavior thinking.
`LC-5` — Dates, GUIDs, or random values in the expected position; usually masks non-determinism.
`LC-6` — Zero negative tests for a function with documented error modes.
`LC-7` — Excessive setup (>20 lines) for a single assertion; either too-many-dependencies SUT or test reconstructs production state instead of isolating behavior.
`LC-8` — Parameterized test where all cases assert the same thing; parameterization isn't doing work.

## Positive signals (POS-*)

`POS-1` — Test name reads as a requirement sentence.
`POS-2` — Expected value has an external source (spec, RFC vector, sample file, domain invariant, linked ticket).
`POS-3` — Assertions on return value or published side effect (not on internal mock invocations).
`POS-4` — Parameterized tests covering boundaries/equivalence classes with *varied* expected values.
`POS-5` — Separate tests for happy path and each distinct sad path.
`POS-6` — Comments citing a requirement, spec, or invariant on non-obvious expected values.
`POS-7` — Test expresses an invariant (round-trip, idempotency, commutativity, associativity, bounds).
