# Test Quality Audit — Deep Mode, Maximum Depth

**Date:** 2026-04-12
**Scope:** All four test projects in [tests/](../../tests/) — `Lfm.App.Core.Tests`, `Lfm.App.Tests`, `Lfm.Api.Tests`, `Lfm.E2E`.
**Extensions loaded:** `dotnet` (xUnit, bUnit, Moq, FluentAssertions, Playwright, Testcontainers, AxeCore).
**Rubric dispatch:** 44 tests under unit rubric (App.Core), 40 under unit/component (App bUnit), 203 under unit (Api), 56 under E2E (split F=34 / A=11 / P=5 / S=16). Zero dispatched to integration rubric — no `WebApplicationFactory<T>` / `HostBuilder` / `TestServer` / `Testcontainers` at integration-seam level detected in Api.Tests (the single Cosmos concurrency test file mocks Cosmos types directly and is unit).

---

## 1. Executive summary

**Overall suite grade: strong, with one significant gap.**

| Project | Tests | Spec | Char / Incidental | Top smells | Grade |
|---|---|---|---|---|---|
| [Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) | 44 | ~32 | ~12 | `HC-5` (mock-verify on same-module locale service), `POS-7` (round-trip invariants) | adequate |
| [Lfm.App.Tests](../../tests/Lfm.App.Tests/) (bUnit) | 40 | 28 | 10 (+2 ambig) | `dotnet.LC-3` (markup-matches literals ×11), `LC-2`, `POS-1` | adequate |
| [Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) | 203 | 198 | 5 | `dotnet.POS-4` (audit-log property assertions), `POS-5` (sad-path coverage), `POS-7` (crypto invariants) | strong |
| [Lfm.E2E](../../tests/Lfm.E2E/) | 56 | 39 | 14 (+1 quarantined, 2 ambig) | `E-HC-F1` (element-presence only ×5), `E-HC-S1` (header-only, no browser enforcement ×5), `E-HC-F3` (wall-clock wait ×2) | strong with caveats |
| **Totals** | **343** | **~297 (86 %)** | **~41 (12 %)** | — | — |

**Headline findings:**

1. **[tests/Lfm.Api.Tests/](../../tests/Lfm.Api.Tests/) is exemplary.** 198 of 203 are specification-driven. The pattern using `TestLogger<T>` capture helper to assert on **audit-log property keys** instead of `Mock<ILogger<T>>.Verify(...)` string matching is a strong `dotnet.POS-4` / `dotnet.POS-5` signal throughout. No integration-rubric tests, no snapshot tests, no mock-of-owned-concrete-class smells, and every function-under-test has happy + distinct sad paths + boundary cases. `FunctionAuthorizationContractTests.cs` encodes an assembly-wide auth-policy invariant as an executable test — high `POS-7` value.
2. **[tests/Lfm.App.Core.Tests/](../../tests/Lfm.App.Core.Tests/) is the mutation-testing target and scored 86.24 %.** But Stryker surfaced a **no-coverage gap the static audit missed**: `RunsClient.CancelSignupAsync` (lines 52–58) has no tests at all. This is the most valuable finding in the audit — static audit only examines files that already have tests, so it cannot see an entire untested method.
3. **[tests/Lfm.E2E/](../../tests/Lfm.E2E/) is strong at the infrastructure layer** (hermetic Testcontainers stack, per-test browser contexts, condition-based waits dominate, accessible-name locators, WCAG 2.2 AA cited). Remaining weaknesses are localized: one `Task.Delay(2000)` in `ProfileSpec::RefreshCharacters_Click_UpdatesFromBattleNet`, implementation selectors in `RunsSpec::CreateRun_SubmitForm_AppearsInList`, and one quarantined-with-skip test in `RunsSpec::DeleteRun_Confirm_RemovedFromList` whose skip reason names "shared browser context" — a scope-signal worth root-causing.
4. **[tests/Lfm.App.Tests/](../../tests/Lfm.App.Tests/) (bUnit) has heavy use of short `Markup.Should().Contain(...)` assertions** (`dotnet.LC-3`). Most are locale-helper-backed and not pasted literals, but two tests couple to CSS internals (`AttendanceRosterSectionTests::Renders_Character_Rows_With_Class_Colored_Names`, `WowClassBadgeTests::Renders_Span_With_Correct_Class_Color`) and are characterization.
5. **Rubric dispatch was unambiguous for every file.** No integration-rubric tests exist in the repository. The closest candidate — `RunsRepositoryConcurrencyTests.cs` — mocks `CosmosClient`/`Container` and is correctly unit-level.

---

## 2. Per-project rollups

### 2.1 [Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) — unit rubric

| File | Tests | Spec | Char | Top smells / signals | Grade |
|---|---|---|---|---|---|
| [Services/GuildClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/GuildClientTests.cs) | 5 | 5 | 0 | POS-1, POS-3, POS-5 | strong |
| [Services/InstancesClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/InstancesClientTests.cs) | 4 | 4 | 0 | POS-2, POS-6 (comment-cited coercion rules) | strong |
| [Services/MeClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/MeClientTests.cs) | 10 | 10 | 0 | POS-5 (every error mode covered) | strong |
| [Services/RunsClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs) | 13 | 13 | 0 | POS-7 (URL-encode invariant), POS-6 (assertion messages) | strong |
| [Auth/AppAuthenticationStateProviderTests.cs](../../tests/Lfm.App.Core.Tests/Auth/AppAuthenticationStateProviderTests.cs) | 9 | 7 | 2 | **HC-5** on L103/L117 (locale side-effect verified by `Mock.Verify`, not by state) | adequate |
| [i18n/LocaleServiceTests.cs](../../tests/Lfm.App.Core.Tests/i18n/LocaleServiceTests.cs) | 8 | 8 | 0 | POS-7 (idempotence, normalization), POS-4 (parameterized boundary) | strong |

Carve-outs applied: `Mock<HttpMessageHandler>` / `Protected().Setup("SendAsync", ...)` is a process-boundary mock (not `HC-5`/`LC-1`); per-test fixtures amortizing HTTP client construction are `dotnet.POS-3`.

**Suite verdict:** adequate. HC-5 on the locale side-effect tests is the single warn-level finding — rewrite those two tests to assert on `localeService.CurrentLocale` state rather than `Mock.Verify(s => s.SetLocale("fi"))`.

### 2.2 [Lfm.App.Tests](../../tests/Lfm.App.Tests/) — unit/component rubric (bUnit)

| File | Tests | Spec | Char | Notes | Grade |
|---|---|---|---|---|---|
| [AttendanceRosterSectionTests.cs](../../tests/Lfm.App.Tests/AttendanceRosterSectionTests.cs) | 4 | 2 | 2 | **CSS-hex assertion** L54; group-label assertion `dotnet.LC-3` | adequate |
| [AuthPagesTests.cs](../../tests/Lfm.App.Tests/AuthPagesTests.cs) | 8 | 8 | 0 | navigation history (state-based), `Loc()` helper reads real JSON | strong |
| [CharactersPagesTests.cs](../../tests/Lfm.App.Tests/CharactersPagesTests.cs) | 7 | 5 | 2 | async + error-state coverage; presence-only assertions at L50/L118 | adequate |
| [GuildPagesTests.cs](../../tests/Lfm.App.Tests/GuildPagesTests.cs) | 5 | 3 | 2 | mock-count on async load is legitimate | adequate |
| [InstancesPageTests.cs](../../tests/Lfm.App.Tests/InstancesPageTests.cs) | 2 | 1 | 1 | no error path coverage | adequate |
| [LayoutTests.cs](../../tests/Lfm.App.Tests/LayoutTests.cs) | 9 | 9 | 0 | explicit a11y smoke (skip-to-content, aria-label) | strong |
| [LocaleParityTests.cs](../../tests/Lfm.App.Tests/LocaleParityTests.cs) | 4 | 4 | 0 | **POS-7** key-parity + empty-value invariants across locale JSON | strong |
| [RunsPagesTests.cs](../../tests/Lfm.App.Tests/RunsPagesTests.cs) | 10 | 7 | 3 | good async+error matrix across 3 pages | adequate |
| [ThemeServiceTests.cs](../../tests/Lfm.App.Tests/ThemeServiceTests.cs) | 8 | 8 | 0 | pure state-based, event-count assertions | strong |
| [WowClassBadgeTests.cs](../../tests/Lfm.App.Tests/WowClassBadgeTests.cs) | 3 | 1 | 2 | **pasted hex colors** `#C69B6D` etc. with no citation to WoW class-color canon | adequate |
| [WowClassesTests.cs](../../tests/Lfm.App.Tests/WowClassesTests.cs) | 2 | 1 | 1 | parameterized colors same issue as above | adequate |

**Suite verdict:** adequate. Carve-out `LC-7` (excessive setup) is suppressed for all bUnit `TestContext`/`RenderWithProviders` patterns. The dominant pattern is short locale-backed markup assertions — idiomatic and low-risk. Two WoW-color files are the only real characterization findings.

### 2.3 [Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) — unit rubric (203 tests across 25 files)

| Group | Files | Tests | Grade | Notes |
|---|---|---|---|---|
| OAuth / Battle.net functions | 7 | ~60 | strong | happy + missing-cookie + state-mismatch + exchange-failure + audit-event matrix, `POS-5` throughout |
| OAuth protocol client ([BlizzardOAuthClientTests.cs](../../tests/Lfm.Api.Tests/BlizzardOAuthClientTests.cs)) | 1 | 11 | strong | **RFC 7636 PKCE test vectors** (POS-2), round-trip protect/unprotect invariants, tamper-detection |
| Crypto ([DataProtectionSessionCipherTests.cs](../../tests/Lfm.Api.Tests/DataProtectionSessionCipherTests.cs)) | 1 | ~8 | strong | **POS-7**: round-trip, non-determinism, key-separation, tamper-rejection |
| Guild / permissions | 3 | ~25 | strong | rank boundaries, stale-roster 1-hour boundary, admin-before-mutation ordering |
| Middleware ([Middleware/](../../tests/Lfm.Api.Tests/Middleware/)) | 5 | ~40 | strong | reflection cache, CORS preflight, rate-limit multi-bucket, X-Forwarded-For edge cases, security-headers-on-failure |
| CRUD functions (Runs, Me, Instances, Specs, Health) | 6 | ~35 | strong | `MockSequence` enforces delete-order for data safety; `dotnet.POS-4` audit events throughout |
| Reference sync pipeline ([ReferenceSyncTests.cs](../../tests/Lfm.Api.Tests/ReferenceSyncTests.cs)) | 1 | ~12 | strong | multi-mode emission, role-mapping variants, fault-tolerance across failure modes |
| Assembly-wide contracts ([FunctionAuthorizationContractTests.cs](../../tests/Lfm.Api.Tests/FunctionAuthorizationContractTests.cs)) | 1 | 2 | strong | reflection-driven invariant: every HTTP function has `[RequireAuth]` or is in the explicit allow-list |
| Repo concurrency ([RunsRepositoryConcurrencyTests.cs](../../tests/Lfm.Api.Tests/RunsRepositoryConcurrencyTests.cs)) | 1 | 2 | strong | etag pass-through + 412→ConcurrencyConflictException translation; unit rubric (Cosmos mocked) |

**Suite verdict: strong.** The characterization count (5 of 203 = 2.5 %) is as low as static audit can go. No mocks of owned concrete classes, no string-matching on log text, no snapshot tests, no wall-clock time assertions (dates constructed via `DateTimeOffset` offsets), no shared mutable fixtures, no disabled assertions.

### 2.4 [Lfm.E2E](../../tests/Lfm.E2E/) — E2E rubric

**Infrastructure audit:** [Infrastructure/SharedStack.cs](../../tests/Lfm.E2E/Infrastructure/SharedStack.cs) + [Infrastructure/StackFixture.cs](../../tests/Lfm.E2E/Infrastructure/StackFixture.cs) implement `E-POS-8` hermetic stack bringup (Testcontainers Cosmos+Azurite, dynamic ports, `WaitForHttp` readiness, deterministic [Seeds/DefaultSeed.cs](../../tests/Lfm.E2E/Seeds/DefaultSeed.cs)). Per-test `IBrowserContext` creation in fixture init gives `E-POS-6`. Page objects under [Pages/](../../tests/Lfm.E2E/Pages/) are narrow (3–10 methods each, single route each) — no `E-LC-5` god-object. Two wall-clock sleeps live in the infra layer ([StackFixture.cs:18](../../tests/Lfm.E2E/Infrastructure/StackFixture.cs#L18) and [StackFixture.cs:261](../../tests/Lfm.E2E/Infrastructure/StackFixture.cs#L261)) during container bringup only — acceptable since they run once per suite.

| Spec | Tests | Sub-lane | Notable findings |
|---|---|---|---|
| [AccessControlSpec.cs](../../tests/Lfm.E2E/Specs/AccessControlSpec.cs) | 8 | F | all specification; route-protection smoke + authenticated-access + public-route coverage |
| [AccessibilitySpec.cs](../../tests/Lfm.E2E/Specs/AccessibilitySpec.cs) | 11 | A | **WCAG 2.2 AA cited** ([AccessibilityHelper.cs:19](../../tests/Lfm.E2E/Helpers/AccessibilityHelper.cs#L19)), no suppressions, keyboard-flow coverage (Tab, focus indicator, Enter/Space). Omits post-interaction rescans — documented at `AccessibilitySpec:405` because the app has no overlay modals. |
| [AuthSpec.cs](../../tests/Lfm.E2E/Specs/AuthSpec.cs) | 5 | F | OAuth intent + test-mode login + logout + error page; 2 presence-only smoke tests (`E-HC-F1`, info) |
| [PerformanceSpec.cs](../../tests/Lfm.E2E/Specs/PerformanceSpec.cs) | 5 | P | **diagnostic / monitoring only** — metrics recorded via [PerfResultCollector.cs](../../tests/Lfm.E2E/Infrastructure/PerfResultCollector.cs), no budget assertions. `E-HC-P1` does not apply (no threshold asserted). Acceptable baseline; `E-HC-P4` (localhost) applies when budgets are added. |
| [ProfileSpec.cs](../../tests/Lfm.E2E/Specs/ProfileSpec.cs) | 7 | F | **`E-HC-F3` warn** at L104 `Page.WaitForTimeoutAsync(2000)` — replace with `WaitForRequestAsync`. One borderline `E-HC-F10` at `SelectCharacter_ViaApi_UpdatesSelection` that could move to integration sub-lane B. |
| [RunsSpec.cs](../../tests/Lfm.E2E/Specs/RunsSpec.cs) | 8 | F | **`E-HC-F2` warn** — implementation-id selectors `Locator("#instance-select")` at `CreateRun_SubmitForm_AppearsInList`. Missing assertion that the newly-created run appears by name (`E-HC-F1`). Misnamed test `CancelSignup_Remove_DisappearsFromRoster` does not actually test removal. **One quarantined test**: `DeleteRun_Confirm_RemovedFromList` with Skip reason "form submission aborted in shared browser context — needs isolated page" → `E-HC-F11` fragile-setup signal + `E-HC-P5` quarantine-without-root-cause. |
| [SecuritySpec.cs](../../tests/Lfm.E2E/Specs/SecuritySpec.cs) | 16 | S | **Five `E-HC-S1` info** findings (X-Content-Type-Options, X-Frame-Options, HSTS, Referrer-Policy, CSP) — tests assert header values but not browser enforcement. Two cookie tests (HttpOnly, SameSite) and the tampered-cookie→401 test are genuine `E-POS-9` (browser-enforced). Two XSS/NoSQL payload tests use pasted literals (`E-HC-S2` info — no OWASP/fuzzing reference). One honestly-documented limitation on DataProtection cookie revocation. |

**Suite verdict:** strong with caveats. The suite's floor is high (hermetic stack, per-test isolation, accessible locators, condition-based waits, WCAG cited) and its ceiling is capped by four specific gaps listed in the worklist below.

---

## 3. Mutation testing

- **Tool:** Stryker.NET 4.14.1 (local tool manifest at [.config/dotnet-tools.json](../../.config/dotnet-tools.json)).
- **Scope:** [Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) → mutates [app/Lfm.App.Core/](../../app/Lfm.App.Core/). Blazor WASM test project [Lfm.App.Tests](../../tests/Lfm.App.Tests/) excluded per documented limitation (Razor source-generator recompile). Azure Functions backend [Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) not mutated in this run — out of scope for the extract-to-Core workaround but eligible in principle. E2E excluded by rubric — no single compile unit.
- **Mutation score:** **86.24 %** (94 killed-equivalent / 109 effective mutants). 160 mutants created, 38 ignored (block-already-covered filter), 13 compile-error, 5 no-coverage, 10 survived.
- **Files with zero direct test coverage** (all mutants `NoCoverage`, `Survived` on uncovered lines, or CompileError):
  - `app/Lfm.App.Core/Services/BattleNetClient.cs` — no test file in `App.Core.Tests/Services/`. 15 killed + 3 survived + 6 compile-error. The killed mutants are being hit transitively, but this file has no dedicated test suite. **Static audit missed this because it only examined files that had a matching test file.**
  - `app/Lfm.App.Core/i18n/JsonStringLocalizer.cs` — no direct tests. 15 killed + 2 survived + 2 no-coverage + 5 compile-error + 11 ignored.
- **Audit-vs-mutation disagreements** (static rated strong + survivors):
  - [Services/RunsClient.cs](../../app/Lfm.App.Core/Services/RunsClient.cs) — audit rated `RunsClientTests.cs` **strong** (13/13 specification). Stryker found **3 no-coverage mutants at lines 54–56**: `CancelSignupAsync` (lines 52–58) **has no tests**. The audit saw tests for `List`, `Get`, `Create`, `Delete`, `Signup`, `Update` and assumed parity with the public API; it did not check that `CancelSignupAsync` had a matching test. This is the single highest-value finding in the audit and the archetypal "static-strong + mutation-survivor" disagreement.
  - [Services/BattleNetClient.cs](../../app/Lfm.App.Core/Services/BattleNetClient.cs) — audit **did not examine** this file because no test file exists. Three `Block removal` survivors at lines 22, 38, 57 — all are `catch (HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException) { return null; }` blocks in `GetCharactersAsync`, `RefreshCharactersAsync`, `GetPortraitsAsync`. Removing these try/catch blocks does not fail any test — the exception paths are never exercised from `Lfm.App.Core.Tests`.
  - [Services/GuildClient.cs](../../app/Lfm.App.Core/Services/GuildClient.cs):16 — audit rated **strong** (5/5 spec). One `Block removal` survivor in the `catch (HttpRequestException)` block. The happy path test kills mutants on the try-body, but the exception-path test (`GetAsync_returns_null_on_HttpRequestException`) is not strong enough to kill a block-removal mutation on the catch body — investigation needed.
  - [Services/MeClient.cs](../../app/Lfm.App.Core/Services/MeClient.cs) — audit rated **strong** (10/10 spec). Two `Block removal` survivors at lines 16 and 31. Same pattern: error-handling blocks whose absence does not flip the assertion result.
  - [Auth/AppAuthenticationStateProvider.cs:54](../../app/Lfm.App.Core/Auth/AppAuthenticationStateProvider.cs#L54) — one `Statement mutation` survivor on `NotifyAuthenticationStateChanged(GetAuthenticationStateAsync())` inside `NotifyStateChanged()`. The cache-invalidation test `NotifyStateChanged_clears_cache_so_next_call_refetches` verifies the next call refetches but **does not verify that Blazor subscribers are re-notified**. Subscribers depending on `NotifyAuthenticationStateChanged` would silently stop receiving updates.
- **Notes:** Blazor WASM SUT correctly excluded per extension limitation. Extract-to-Core refactor already applied ([app/Lfm.App.Core/](../../app/Lfm.App.Core/)), so no `P3` refactor recommendation.

---

## 4. Suite assessment

**Overall verdict: strong** (with one gap and six warn-level fixups).

**Top risks, by impact:**

1. **`RunsClient.CancelSignupAsync` has no tests at all.** Static audit missed it; mutation testing caught it. This is the highest-value finding: the method is in production and used by the E2E flow `RunsSpec::CancelSignup_Remove_DisappearsFromRoster` (which is itself misnamed and doesn't test removal). A silent regression could land and nothing below the E2E layer would notice.
2. **Locale side-effect assertions via `Mock.Verify`** in `AppAuthenticationStateProviderTests` (L103, L117) — the single characterization finding in App.Core that is both audit-visible and mutation-relevant. Rewrite to state verification.
3. **E2E quarantine signal** in `RunsSpec::DeleteRun_Confirm_RemovedFromList` — the skip reason names the shared browser context, which points at `E-HC-F11` fragile setup. A growing quarantine list at the E2E layer is a scope signal per the rubric; this is currently a single test but worth root-causing before it becomes a pattern.
4. **Five `E-HC-S1` header-only security tests** in `SecuritySpec` — all are characterization at the E2E layer when they could be proved cheaper in integration sub-lane B. Keep as smoke tests; do not expand the pattern. Add one genuine browser-enforcement test per policy (e.g. inject a `<script>` and verify CSP blocks it).
5. **BattleNetClient has no unit tests in App.Core.Tests.** Three surviving mutants on exception-catch blocks are the direct evidence. The API-side `BattleNetCharactersFunctionTests` in Api.Tests is a different surface.

**Verification limits** (neither static audit nor mutation testing can determine):

- Whether the expected values in any test reflect the **real domain spec** (OAuth request shapes, guild permission semantics, audit-event property schemas). Audit marked these as `spec` based on plausibility and comment-backed assertions, not external verification.
- **Git provenance** — whether tests were written before or after the implementation. CLAUDE.md does not mandate TDD and no commit metadata was checked.
- **E2E flake history** — static audit sees one quarantined test, but historical flake rate across the remaining 55 is unknown.
- **Perf budgets** — `PerformanceSpec` records but does not assert. Whether the recorded metrics are within a defensible budget cannot be answered without a Web Vitals / RUM / SLO source.
- **Contract coverage** — no consumer-driven contract tests exist for the Blizzard OAuth / Cosmos seams, which is fine at this scale but means regressions in those external contracts will not surface in this suite.

---

## 5. Prioritized remediation worklist

### P0 — address before next release

**P0.1** **Add tests for `RunsClient.CancelSignupAsync`** `[mutation]`
Stryker's 3 no-coverage mutants at [app/Lfm.App.Core/Services/RunsClient.cs:54-56](../../app/Lfm.App.Core/Services/RunsClient.cs#L54-L56). Mirror the existing `SignupAsync` tests in [tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs): happy path (DELETE to `/runs/{id}/signup` returns 200 with RunDetailDto), null on non-2xx, URL-encoding boundary. Effort: ~30 min.

### P1 — fix this iteration

**P1.1** **Rewrite locale side-effect tests in `AppAuthenticationStateProviderTests`** (L103, L117).
Replace `Mock<ILocaleService>.Verify(s => s.SetLocale("fi"), Times.Once)` with a real `LocaleService` instance (or a capturing fake) and assert `localeService.CurrentLocale == "fi"` after `GetAuthenticationStateAsync()` returns. Kills the `HC-5` verdict and removes the coupling to internal invocation order. Effort: ~20 min.

**P1.2** **Fix wall-clock wait in `ProfileSpec::RefreshCharacters_Click_UpdatesFromBattleNet` (L104)**.
Replace `Page.WaitForTimeoutAsync(2000)` with `Page.WaitForRequestAsync("**/api/battlenet/characters/refresh")` or an assertion on the post-refresh UI state. `E-HC-F3` warn. Effort: ~15 min.

**P1.3** **Replace implementation-id selectors in `RunsSpec::CreateRun_SubmitForm_AppearsInList`**.
`Locator("#instance-select")` and `Locator("#instance-select fluent-option")` → `GetByRole(AriaRole.Combobox, ...)` and `GetByRole(AriaRole.Option, ...)`. If the FluentUI select does not expose proper ARIA, open a tracking issue and keep the selector until the upstream component is fixed. `E-HC-F2` warn. Effort: ~15 min.

**P1.4** **Add the missing "created run appears by name" assertion** in `RunsSpec::CreateRun_SubmitForm_AppearsInList` (L92).
The test creates a unique run name but only asserts the list count is ≥ 1 — a pre-existing run would satisfy that. Assert the run element by `GetByText(uniqueRunName)`. `E-HC-F1`. Effort: ~10 min.

**P1.5** **Root-cause the quarantined `RunsSpec::DeleteRun_Confirm_RemovedFromList`**.
The skip reason names shared browser context. Confirm whether the test needs a fresh `IBrowserContext` (the other tests in the file already use one — check why this one is different) and remove the `[Skip]`. If the flake is real under isolation, file an upstream bug against the form submission path in Blazor WASM. `E-HC-F11` + `E-HC-P5`. Effort: ~30–60 min.

### P2 — address when touching adjacent code

**P2.1** **Add direct unit tests for `BattleNetClient`** `[mutation]`
The three `Block removal` survivors at lines 22, 38, 57 prove the exception-catch paths are never exercised. Add tests for `GetCharactersAsync`, `RefreshCharactersAsync`, `GetPortraitsAsync` along the same shape as [Services/GuildClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/GuildClientTests.cs) — happy path, `HttpRequestException` → null, `TaskCanceledException` → null, non-success status → null. Effort: ~45 min.

**P2.2** **Strengthen the `GetAsync_returns_null_on_HttpRequestException` assertion in `GuildClientTests`** `[mutation]`
The one `Block removal` survivor at [GuildClient.cs:16](../../app/Lfm.App.Core/Services/GuildClient.cs#L16) means the catch body is under-constrained. Investigate Stryker's mutation detail (likely a block-body removal that keeps the method returning `default(GuildDto)` instead of explicit `null`) and tighten the assertion. Effort: ~20 min.

**P2.3** **Strengthen `MeClient` exception-path tests** `[mutation]`
Two `Block removal` survivors at lines 16 and 31. Same investigation pattern as P2.2. Effort: ~20 min.

**P2.4** **Add a `NotifyAuthenticationStateChanged` subscriber-notification assertion** `[mutation]`
`AppAuthenticationStateProvider.cs:54` survivor: subscribers never verified. Add a bUnit or plain unit test that subscribes to `AuthenticationStateChanged` and asserts the handler fires exactly once after `NotifyStateChanged()`. Effort: ~20 min.

**P2.5** **Document the WoW class-color canon** in `WowClassBadgeTests` and `WowClassesTests`.
Add a top-of-file comment citing Blizzard's class-color reference (the public WoW API field or the community design source). Removes the `HC-3` pasted-literal verdict. Effort: ~10 min.

**P2.6** **Rename `RunsSpec::CancelSignup_Remove_DisappearsFromRoster`** to reflect what it actually tests (roster rendering with a seeded character) — or implement the removal action. Current name lies about intent. Effort: ~10 min.

### P3 — backlog / worth tracking

**P3.1** **Add at least one browser-enforcement test per security policy** in `SecuritySpec`. For each of CSP, X-Frame-Options, CORS, HSTS: write one test that actually invokes the browser's enforcement path (e.g. CSP: inject a `<script>` via a route and verify the browser console records the block; X-Frame-Options: attempt to iframe the app from a cross-origin page). Keep the header-value smoke tests. Effort: ~2 h.

**P3.2** **Establish perf budgets in `PerformanceSpec`** once a Web Vitals / RUM baseline exists. Until then, the monitoring-only tests are correct. When budgets land, cite the source in the test body and use p75 across N runs (not single-sample) to avoid `E-HC-P2`. Effort: proportional to baseline work.

**P3.3** **Add a fuzzing corpus** to the XSS and NoSQL injection tests (`SecuritySpec` L275, L306). Link to OWASP WSTG payload lists or a short corpus file; parameterize the test. Effort: ~1 h.

**P3.4** **Run Stryker against `Lfm.Api.Tests`** as a follow-up validation. The Api project is `Microsoft.NET.Sdk` (not Web) and the test project is plain SDK — it should be eligible. Expected value: validate the already-strong static audit verdict against mutation survivors, especially in the crypto, OAuth, and middleware paths. Command: `cd tests/Lfm.Api.Tests && dotnet stryker --reporter json --reporter cleartext`. Effort: unattended run + 30 min review.

---

## 6. Final notes

- **Static + mutation together caught more than either alone.** Static flagged the locale-side-effect `HC-5` that mutation could not grade; mutation found the `CancelSignupAsync` no-coverage gap that static could not see. The audit-vs-mutation disagreements on GuildClient / MeClient / BattleNetClient `Block removal` survivors are the classic "file rated strong with survivors" finding the skill is designed to surface.
- **E2E was correctly excluded from mutation testing** per the skill rule. The sub-lane classification (F/A/P/S) dispatched cleanly because the project uses explicit `[Trait("Category", ...)]` metadata and distinct `Helpers/` namespaces.
- **No files were modified during the audit.** The only side effect is the `StrykerOutput/2026-04-12.03-05-38/` directory, which is already gitignored.
