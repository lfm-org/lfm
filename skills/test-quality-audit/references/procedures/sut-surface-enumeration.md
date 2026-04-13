# SUT surface enumeration

**When this runs:** step 2.5 of the deep-mode workflow in [../../SKILL.md](../../SKILL.md). This is the skill's static gap-detection pass — *find tests that don't exist yet* for public API, HTTP routes, migrations, throw sites, and validation attributes in the SUT. Never run in quick mode. Never run against E2E audit targets.

Unlike mutation testing (step 4) which observes runtime kill behavior, surface enumeration reads the production code to ask "what ought to be tested?" and cross-references against the test project. Both are complementary — mutation testing catches tests that execute code without verifying it; surface enumeration catches code that has no test at all.

## Why both static surface enumeration and mutation testing

- **Static audit alone** only examines files that already have tests.
- **Mutation testing** catches `NoCoverage` files at runtime, but only when the tool is installed and the SUT shape is supported (e.g. Stryker.NET cannot mutate Blazor WASM — see [../../extensions/dotnet-core.md § Known SUT limitations](../../extensions/dotnet-core.md)).
- **Static surface enumeration** works on any SUT the stack extension has grep patterns for, even Blazor WASM. It produces *probable* gaps from grep, so it is noisier than mutation testing.
- When both run on the same suite, a symbol flagged by both is a **confirmed gap**; a symbol flagged by static-only is a *probable* gap; a symbol flagged by mutation-only is a signal the grep patterns need tuning.

## Procedure

1. **Read the extension's SUT surface enumeration section.** Every extension that supports gap detection must provide: SUT identification instructions, grep patterns per gap class (API / routes / migrations / throw sites / validation), and cross-reference matching rules. Extensions without this section produce no gap report — record the skip reason and continue with static findings only.
2. **Identify the SUT.** For each test project in scope, walk its `<ProjectReference>` closure (or equivalent module graph) to the production project(s) under test. Record the SUT project list in the gap-report header. A test project may have multiple SUT projects (e.g. a shared contract library plus the API that consumes it).
3. **Enumerate testable surface** by running the extension's grep patterns against the SUT source tree. For each pattern, capture:
   - A symbol identifier (method name, route template, migration class name, exception-throwing method name, validation-attribute target property).
   - A source location (file path and line number).
4. **Cross-reference against test discovery.** For each enumerated symbol, check whether its identifier appears in at least one test file. A test "covers" a symbol when:
   - Its identifier appears in a test method name or attribute (`[Fact(DisplayName = "...")]`).
   - Its identifier appears in a test body as an invocation target, a string literal (for routes), or a type reference (for migrations / exceptions).
   - An indirect-coverage signal fires (e.g. a controller test exercises a service method indirectly — the extension may document known indirect-coverage patterns that suppress false positives).
5. **Emit a gap report** (see [§ Gap report format](#gap-report-format) below). Each unreferenced entry is a *probable* gap because grep is an approximate cross-reference. A true negative requires either mutation testing or manual verification.
6. **On extension section missing**, report: "Gap detection skipped — stack extension has no SUT surface enumeration section. Mutation testing remains the only gap-finding mechanism for this scope." Continue with static findings.

## Gap classes

The extension's patterns must populate these five categories. Extensions may add their own categories (e.g. `Gap-Resource` for a REST resource that has no test class) as long as they document them.

- **`Gap-API`** — public type or method with no test reference. *Medium confidence:* indirect coverage via a caller is common.
- **`Gap-Route`** — HTTP route / function handler / message-queue handler with no test reference to its route template, queue name, or topic. *High confidence:* routes are registered by string identity; a test that doesn't mention the string almost certainly doesn't cover it.
- **`Gap-Migration`** — database migration class (or file) with no test reference to its class name. *High confidence.*
- **`Gap-Throw`** — exception throw site with no test that both names the exception type and calls the containing method. *Medium confidence:* may be covered by a generic "error path" test that doesn't name the type.
- **`Gap-Validate`** — a validation attribute (`[Required]`, `[StringLength]`, etc.) or custom validator on an input type with no test that sends a bad value for that field. *High confidence* on serialization-layer input types.

## Rules

- **Extensions own the grep patterns.** The core workflow is framework-neutral; language-specific patterns belong in `extensions/<stack>.md`.
- **Gap detection is suite-level.** Never run step 2.5 in quick mode — a PR-diff or single-file audit produces noise.
- **Never treat a probable gap as a confirmed gap** without verification. The report must flag each finding as probable and recommend mutation testing or manual review for confirmation.
- **Reconcile with mutation testing** in step 5 when both steps produced output.

## Gap report format

In the step-5 suite assessment, emit a `### Gap report` subsection in one of two states:

**State A — enumeration ran:**

```markdown
### Gap report (static SUT surface enumeration)

- **SUT projects:** <project list>
- **Method:** grep-based cross-reference from test files to SUT symbols via the stack extension's patterns. Weak signal — each finding is a *probable* gap and requires verification.

| Class | Enumerated | Referenced from tests | Probable gaps | Confidence |
|---|---|---|---|---|
| `Gap-API` — public methods | <N> | <M> | <N-M> | medium |
| `Gap-Route` — HTTP / function routes | <N> | <M> | <N-M> | high |
| `Gap-Migration` — migration classes | <N> | <M> | <N-M> | high |
| `Gap-Throw` — throw sites | <N> | <M> | <N-M> | medium |
| `Gap-Validate` — validation attributes | <N> | <M> | <N-M> | high |

#### Top probable gaps (highest confidence first)

- **`Gap-Route`** — `DELETE /api/orders/{id}` (`api/Functions/OrdersApi.cs:88`): route registered, no test references the template. Likely true gap.
- **`Gap-Migration`** — `AddOrderStatusColumnMigration` (`api/Migrations/0007_order_status.cs:14`): migration class name not mentioned in any test.
- **`Gap-Validate`** — `[Required] CustomerId` on `CreateOrderRequest` (`shared/Requests.cs:22`): no test sends a request with missing `CustomerId`.
- **`Gap-API`** — `OrderService.CancelOrderAsync` (`api/Services/OrderService.cs:42`): public, no test body references it. *Verify:* may be covered via a caller.
- **`Gap-Throw`** — `throw new InvalidOperationException("Order already shipped")` (`api/Services/OrderService.cs:142`): no test names both the exception type and the method.

#### Reconciliation with mutation testing (when step 4 produced results)

- **Confirmed gaps** (static probable gap ∩ mutation `NoCoverage`): <list>
- **Static-only probable gaps** (mutation saw runtime coverage): <list with note that indirect coverage exists>
- **Mutation-only gaps** (no grep match but static cross-reference failed): <list — signal to tune the grep patterns in the extension>
```

**State B — enumeration skipped:**

```markdown
### Gap report

- **Status:** skipped — <extension name> has no `SUT surface enumeration` section (or: target is quick mode / target is E2E-only).
- **What this means:** static gap detection is unavailable for this scope. Mutation testing (if applicable) is the only gap-finding mechanism. Consider writing an extension section to enable surface enumeration.
```
