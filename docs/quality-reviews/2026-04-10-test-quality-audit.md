# Test Quality Audit — `tests/Lfm.Api.Tests` + `tests/Lfm.App.Tests`

**Date:** 2026-04-10
**Mode:** deep
**Extensions loaded:** `dotnet` (xUnit + Moq + FluentAssertions + bUnit)
**Scope:** 53 unit-test files, ~239 tests
**Rubric:** [docs/quality-reference/unit-testing.md](../quality-reference/unit-testing.md) + [.claude/skills/test-quality-audit/extensions/dotnet.md](../../.claude/skills/test-quality-audit/extensions/dotnet.md)

## Infrastructure verified first

- [tests/Lfm.Api.Tests/TestLogger.cs](../../tests/Lfm.Api.Tests/TestLogger.cs) is a **capture-helper fake** with `IsAudit()` for structured-property assertions → `dotnet.POS-4 + dotnet.POS-5` (positive). Every `Mock<ILogger<T>>`-looking pattern in these tests is actually a fake; do not flag.
- [tests/Lfm.App.Tests/RenderWithProviders.cs](../../tests/Lfm.App.Tests/RenderWithProviders.cs) → `ComponentTestBase : BunitContext` with real `FileStringLocalizer` reading the production locale JSON files. `Loc("key")` assertions are **spec-derived from the locale file** (positive).
- [tests/Lfm.App.Tests/Services/StubHttpMessageHandler.cs](../../tests/Lfm.App.Tests/Services/StubHttpMessageHandler.cs) is a **test-only fake at the HTTP boundary** (not a Moq mock). Assertions on `LastRequest` → `POS-3`.

## Per-file rollup

### Api.Tests (35 files, ~159 tests)

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| [RunsCancelSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsCancelSignupFunctionTests.cs) | 4 | 3 | 1 | 0 | `dotnet.HC-3` (attr test) | strong |
| [RunsCreateFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsCreateFunctionTests.cs) | 5 | 4 | 1 | 0 | `dotnet.HC-3` | strong |
| [RunsDeleteFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsDeleteFunctionTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [RunsDetailFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsDetailFunctionTests.cs) | 4 | 3 | 1 | 0 | `dotnet.HC-3` | strong |
| [RunsListFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsListFunctionTests.cs) | 3 | 2 | 1 | 0 | `dotnet.HC-3` | strong |
| [RunsSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs) | 8 | 6 | 1 | 1 | `dotnet.HC-1`, `HC-6` | adequate |
| [RunsUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsUpdateFunctionTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [BattleNetCallbackFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCallbackFunctionTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [BattleNetCharacterPortraitsFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharacterPortraitsFunctionTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [BattleNetCharactersFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharactersFunctionTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [BattleNetCharactersRefreshFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharactersRefreshFunctionTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [BattleNetLoginFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetLoginFunctionTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [BattleNetLogoutFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetLogoutFunctionTests.cs) | 3 | 2 | 1 | 0 | `HC-4` | adequate |
| [BlizzardOAuthClientTests.cs](../../tests/Lfm.Api.Tests/BlizzardOAuthClientTests.cs) | 8 | 8 | 0 | 0 | — | strong |
| [MeFunctionTests.cs](../../tests/Lfm.Api.Tests/MeFunctionTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [MeUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/MeUpdateFunctionTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [MeDeleteFunctionTests.cs](../../tests/Lfm.Api.Tests/MeDeleteFunctionTests.cs) | 3 | 2 | 1 | 0 | `HC-6` | adequate |
| [GuildAdminFunctionTests.cs](../../tests/Lfm.Api.Tests/GuildAdminFunctionTests.cs) | 5 | 4 | 0 | 1 | `dotnet.HC-6` | strong |
| [GuildFunctionTests.cs](../../tests/Lfm.Api.Tests/GuildFunctionTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [HealthFunctionTests.cs](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs) | 4 | 2 | 2 | 0 | `HC-3`, `HC-10` | adequate |
| [InstancesListFunctionTests.cs](../../tests/Lfm.Api.Tests/InstancesListFunctionTests.cs) | 1 | 0 | 1 | 0 | `HC-3` | weak |
| [PrivacyContactFunctionTests.cs](../../tests/Lfm.Api.Tests/PrivacyContactFunctionTests.cs) | 6 | 6 | 0 | 0 | — | strong |
| [RaiderCharacterFunctionTests.cs](../../tests/Lfm.Api.Tests/RaiderCharacterFunctionTests.cs) | 3 | 2 | 0 | 1 | `dotnet.HC-6` | strong |
| [RaiderCleanupFunctionTests.cs](../../tests/Lfm.Api.Tests/RaiderCleanupFunctionTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [SpecializationsListFunctionTests.cs](../../tests/Lfm.Api.Tests/SpecializationsListFunctionTests.cs) | 2 | 0 | 1 | 1 | `HC-3` | weak |
| [WowUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/WowUpdateFunctionTests.cs) | 4 | 2 | 2 | 0 | `HC-3` | adequate |
| [Middleware/RateLimitMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/RateLimitMiddlewareTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [Middleware/SecurityHeadersMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [AuditLogTests.cs](../../tests/Lfm.Api.Tests/AuditLogTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [DataProtectionSessionCipherTests.cs](../../tests/Lfm.Api.Tests/DataProtectionSessionCipherTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [GuildPermissionsTests.cs](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs) | 15 | 14 | 0 | 1 | `LC-6` gap | adequate |
| [RunEditabilityTests.cs](../../tests/Lfm.Api.Tests/RunEditabilityTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [RunsRepositoryConcurrencyTests.cs](../../tests/Lfm.Api.Tests/RunsRepositoryConcurrencyTests.cs) | 2 | 2 | 0 | 0 | — | strong |
| [WowClassesTests.cs](../../tests/Lfm.Api.Tests/WowClassesTests.cs) | 6 | 6 | 0 | 0 | — | strong |

### App.Tests (18 files, ~80 tests)

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| [AttendanceRosterSectionTests.cs](../../tests/Lfm.App.Tests/AttendanceRosterSectionTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [AuthPagesTests.cs](../../tests/Lfm.App.Tests/AuthPagesTests.cs) | 8 | 4 | 4 | 0 | `HC-1` | **weak** |
| [CharactersPagesTests.cs](../../tests/Lfm.App.Tests/CharactersPagesTests.cs) | 7 | 5 | 2 | 0 | `HC-3` | adequate |
| [GuildPagesTests.cs](../../tests/Lfm.App.Tests/GuildPagesTests.cs) | 6 | 4 | 2 | 0 | `HC-1`, `HC-3` | adequate |
| [InstancesPageTests.cs](../../tests/Lfm.App.Tests/InstancesPageTests.cs) | 2 | 2 | 0 | 0 | — | strong |
| [RunsPagesTests.cs](../../tests/Lfm.App.Tests/RunsPagesTests.cs) | 10 | 9 | 1 | 0 | `HC-3` | strong |
| [LayoutTests.cs](../../tests/Lfm.App.Tests/LayoutTests.cs) | 9 | 9 | 0 | 0 | — | strong |
| [WowClassBadgeTests.cs](../../tests/Lfm.App.Tests/WowClassBadgeTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [Services/RunsClientTests.cs](../../tests/Lfm.App.Tests/Services/RunsClientTests.cs) | 9 | 9 | 0 | 0 | — | strong |
| [ThemeServiceTests.cs](../../tests/Lfm.App.Tests/ThemeServiceTests.cs) | 8 | 8 | 0 | 0 | — | strong |
| [ToastHelperTests.cs](../../tests/Lfm.App.Tests/ToastHelperTests.cs) | 2 | 0 | 2 | 0 | `HC-2`, `dotnet.HC-2` | **weak** |
| [WowClassesTests.cs](../../tests/Lfm.App.Tests/WowClassesTests.cs) | 2 | 2 | 0 | 0 | — | strong |
| [LocaleParityTests.cs](../../tests/Lfm.App.Tests/LocaleParityTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [i18n/JsonStringLocalizerTests.cs](../../tests/Lfm.App.Tests/i18n/JsonStringLocalizerTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [i18n/LocaleServiceTests.cs](../../tests/Lfm.App.Tests/i18n/LocaleServiceTests.cs) | 6 | 6 | 0 | 0 | — | strong |

## Noteworthy per-test findings (severity ≥ warn)

Specification tests with verdict `keep, info` are omitted to stay signal-dense. All findings below are blockers or warns.

### Block (must fix)

#### [ToastHelperTests.cs::ShowSuccess_Delegates_To_ToastService (L11)](../../tests/Lfm.App.Tests/ToastHelperTests.cs#L11)
- **Intent:** no stateable intent — only verifies the null-padded signature `(message, null, null, null)` is forwarded.
- **Provenance:** pasted-literal (the `null, null, null` is the `IToastService` contract shape, not a requirement)
- **Assertion target:** internal-mock-invocation
- **Smells:** `HC-2`, `dotnet.HC-2`, `LC-1` (mocking owned wrapper's own collaborator)
- **Verdict:** characterization · **Severity:** block · **Action:** `rewrite-from-requirement` or `delete`
- **Note:** The `ToastHelper` wrapper adds no behavior; the test locks in the adapter signature. Either delete (thin wrapper doesn't need a test) or rewrite to assert "a toast with Intent=Success containing the message is displayed" via a fake `IToastService` capture helper.

#### [ToastHelperTests.cs::ShowError_Delegates_To_ToastService (L22)](../../tests/Lfm.App.Tests/ToastHelperTests.cs#L22)
- Same as above. **Action:** `rewrite-from-requirement` or `delete`.

### Warn (characterization / brittle coupling)

#### [AuthPagesTests.cs::LandingPage_Renders_Without_Crash (L13)](../../tests/Lfm.App.Tests/AuthPagesTests.cs#L13)
- **Intent:** no stateable intent — asserts `cut.Markup.Should().NotBeEmpty()`.
- **Smells:** `HC-1` · **Verdict:** characterization · **Severity:** warn · **Action:** `delete` or add an assertion on a documented user-visible element (locale-keyed heading, CTA button).

#### [AuthPagesTests.cs::LoginFailedPage_Renders_Without_Crash (L71)](../../tests/Lfm.App.Tests/AuthPagesTests.cs#L71)
- Same shape. `HC-1` · warn · `delete` or assert on the error messaging the user should see.

#### [AuthPagesTests.cs::GoodbyePage_Renders_Without_Crash (L79)](../../tests/Lfm.App.Tests/AuthPagesTests.cs#L79)
- Same shape. `HC-1` · warn · `delete` or assert on the goodbye copy.

#### [AuthPagesTests.cs::PrivacyPolicyPage_Renders_Without_Crash (L88)](../../tests/Lfm.App.Tests/AuthPagesTests.cs#L88)
- Same shape. `HC-1` · warn · `delete` or assert on a locale-keyed section heading.

#### [GuildPagesTests.cs::GuildAdminPage_Renders_Without_Crash (L90)](../../tests/Lfm.App.Tests/GuildPagesTests.cs#L90)
- Same shape. `HC-1` · warn · `delete` (the test immediately below already asserts on the title via `Loc`, so this one is redundant).

#### [RunsSignupFunctionTests.cs::Run_retries_on_concurrency_conflict_and_succeeds (L349)](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs#L349)
- **Intent:** On 1st-call concurrency conflict, retry and succeed.
- **Assertion target:** internal-mock-invocation (`Times.Exactly(2)` on both `GetByIdAsync` and `UpdateAsync`)
- **Smells:** `dotnet.HC-1`, `HC-6` · **Verdict:** characterization · **Severity:** warn
- **Action:** `rewrite-from-requirement` — the observable outcome is "200 with updated run" after one conflict; assert the final `OkObjectResult` payload reflects the retried state and drop the `Times.Exactly(2)` pin. The retry count is a loop-structure detail.

#### [RunsSignupFunctionTests.cs::Run_returns_409_after_exhausting_concurrency_retries (L377)](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs#L377)
- **Smells:** `dotnet.HC-1`, `HC-6` · **Severity:** warn
- **Action:** keep the `409` assertion, drop the redundant `Times.Exactly` verifications (the visible 409 already proves exhaustion).

#### [BattleNetLogoutFunctionTests.cs::Run_clears_auth_cookie_and_redirects_to_app_base_url (L61)](../../tests/Lfm.Api.Tests/BattleNetLogoutFunctionTests.cs#L61)
- **Smells:** `HC-4` — the assertion uses `if (max-age==0) ... else if (expires=past)` branching inside the test body to accept either cookie-deletion encoding.
- **Severity:** warn · **Action:** `split` into two deterministic tests, or assert declaratively that the returned `Set-Cookie` has `Max-Age=0` (pick whichever shape the SUT actually produces and pin it).

#### [MeDeleteFunctionTests.cs::Returns_ok_and_calls_both_repos_in_order_when_raider_exists (L44)](../../tests/Lfm.Api.Tests/MeDeleteFunctionTests.cs#L44)
- **Intent:** Scrub-then-delete ordering is a real requirement (data-safety invariant: scrub references before deleting identity).
- **Smells:** `HC-6` — uses `Callback` + a `List<string> callOrder` to track invocation order.
- **Severity:** warn · **Action:** replace with Moq `MockSequence` / `InSequence` or a small fake that throws if called out of order. The *requirement* is fine; the mechanism is fragile.

#### [HealthFunctionTests.cs::Ready_returns_503_with_unready_when_cosmos_throws_cosmos_exception (L65)](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs#L65)

#### [HealthFunctionTests.cs::Ready_returns_503_with_exception_type_name_for_unexpected_errors (L79)](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs#L79)
- **Smells:** `HC-3`, `HC-10` — the error response shape `{ status = "unready", error = nameof(CosmosException) }` is asserted as a pasted anonymous object with no spec link.
- **Severity:** warn · **Action:** document the health-probe error contract (readme line or xmldoc on the function) and cite it; OR weaken to `status.Should().Be("unready")` and stop pinning the error-type field.

#### [InstancesListFunctionTests.cs::Returns_instances_from_repository (L15)](../../tests/Lfm.Api.Tests/InstancesListFunctionTests.cs#L15)

#### [SpecializationsListFunctionTests.cs::Returns_specializations_from_repository (L16)](../../tests/Lfm.Api.Tests/SpecializationsListFunctionTests.cs#L16)

#### [WowUpdateFunctionTests.cs::Returns_200_with_sync_results_when_caller_is_site_admin (L42)](../../tests/Lfm.Api.Tests/WowUpdateFunctionTests.cs#L42)

#### [WowUpdateFunctionTests.cs::Returns_200_with_partial_results_when_one_entity_fails (L103)](../../tests/Lfm.Api.Tests/WowUpdateFunctionTests.cs#L103)
- **Smells:** `HC-3` — DTO expected values are pasted literals (`"liberation-of-undermine"`, `"Holy"`/`65`/`2`, `"synced (12 docs)"`, `"failed: Blizzard API returned 503"`).
- **Severity:** warn · **Action:** extract a shared test-fixture builder or assert against the exact object returned by the stubbed repo (i.e. build the expected list once and compare). The "synced (N docs)" string in `WowUpdateFunctionTests` is particularly fragile and should either become a structured field or be de-stringified.

#### [CharactersPagesTests.cs::CharactersPage_Renders_Error_State_On_Failure (L87)](../../tests/Lfm.App.Tests/CharactersPagesTests.cs#L87)

#### [CharactersPagesTests.cs::CharactersPage_Renders_Error_When_Client_Returns_Null (L103)](../../tests/Lfm.App.Tests/CharactersPagesTests.cs#L103)

#### [GuildPagesTests.cs::GuildPage_Renders_Error_When_Client_Returns_Null (L73)](../../tests/Lfm.App.Tests/GuildPagesTests.cs#L73)

#### [RunsPagesTests.cs::EditRunPage_Shows_Error_When_Run_Not_Found (L195)](../../tests/Lfm.App.Tests/RunsPagesTests.cs#L195)
- **Smells:** `HC-3` — hardcoded literals `"Failed to load characters."`, `"Failed to load guild data."`, `"Run not found."` with no locale-file provenance.
- **Severity:** warn (one per file) · **Action:** move the strings to the locale JSON files and assert via `Loc("characters.error.loadFailed")` etc. This aligns these error-path tests with the happy-path pattern already used elsewhere in the same files.

#### [GuildPermissionsTests.cs::CanCreateGuildRunsAsync_honours_explicit_rank_permission_entry (L189)](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs#L189)
- **Smells:** `LC-6` — no test covers the case where some ranks have explicit entries and the queried rank does not (does it fall back to default, or inherit from a nearby rank?).
- **Severity:** info · **Action:** `add-assertion` for the mixed-explicit/default edge case.

### Pattern-level observation — `Run_method_has_RequireAuth_attribute`

~9 files carry a single-line `[Fact]` doing reflection over the `Run` method to assert `[RequireAuth]` is present. These hit `dotnet.HC-6` / `HC-7` (structural-only; name describes HOW).

- **Verdict:** ambiguous — they encode a real security invariant (the function requires auth) but at a brittle level (the *attribute*, not the *behavior*).
- **Severity:** info · **Action:** `keep` for now, but **consider consolidating** into a single assembly-wide `[Theory]` iterating all function classes and asserting that every public `Run` method either has `[RequireAuth]` or appears on an explicit allow-list (login, health, specializations-list, battle-net-login). That single test replaces nine duplicates and catches newly-added unprotected endpoints automatically.

## Suite assessment

- **Overall verdict:** **strong**. Roughly **206 / 239 (86%)** tests are specification-grade. Characterization and warn-level findings are concentrated in two files (`AuthPagesTests`, `ToastHelperTests`) and a short tail of pasted-literal DTO tests.
- **Standout positives:**
  1. **Audit logging is exemplary.** `TestLogger<T>` + `IsAudit(...)` gives every audit-emitting function clean `dotnet.POS-4 / POS-5` coverage across `RunsCreate`, `RunsDelete`, `RunsUpdate`, `RunsSignup`, `RunsCancelSignup`, `BattleNetCallback`, `BattleNetLogout`, `MeDelete`, `RaiderCleanup`, `GuildFunction`, `PrivacyContact`.
  2. **Domain-logic tests nail boundaries.** [RunEditabilityTests.cs](../../tests/Lfm.Api.Tests/RunEditabilityTests.cs), [GuildPermissionsTests.cs](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs), [DataProtectionSessionCipherTests.cs](../../tests/Lfm.Api.Tests/DataProtectionSessionCipherTests.cs), [WowClassesTests.cs](../../tests/Lfm.Api.Tests/WowClassesTests.cs), [BlizzardOAuthClientTests.cs](../../tests/Lfm.Api.Tests/BlizzardOAuthClientTests.cs) — all boundary-complete, spec-cited, invariant-style (round-trip, tamper detection, key isolation, PKCE against RFC 7636 test vectors).
  3. **Middleware tests assert on published side-effects,** not interaction counts.
  4. **App-side tests lean on `Loc()` against real locale JSON** — spec-derived strings, not pasted literals.
  5. **[RunsClientTests.cs](../../tests/Lfm.App.Tests/Services/RunsClientTests.cs)** is a model for HTTP-boundary testing with `StubHttpMessageHandler` + `LastRequest` assertions.

- **Top risks (ordered by impact):**
  1. **`AuthPagesTests` is half render-without-crash** — four of eight tests will pass against any non-blank render, including a broken page. Highest-value fix: they protect your entire unauth surface today.
  2. **`ToastHelperTests` is pure characterization of an adapter signature** — locks in `null, null, null` padding; will break on any signature tidying without catching any real regression.
  3. **Pasted-DTO tests** (`InstancesList`, `SpecializationsList`, `WowUpdate`, `Health` error shape) pin format details that aren't documented anywhere. They won't fail when the format is wrong — only when it changes.
  4. **Retry-count verification in `RunsSignup`** couples to loop iteration counts; a perfectly valid move to an exponential-backoff loop or a policy library would break these for no behavior change.
  5. **Nine near-duplicate `[RequireAuth]` reflection tests** — low blast radius but a consolidation opportunity that also turns the pattern into a coverage ratchet (new unprotected endpoint → test fails automatically).

- **Verification limits:** Static audit cannot confirm (a) whether the *expected literal values* in DTO tests are correct per any real contract; (b) whether mutation testing would reveal additional shallow coverage; (c) git history (was a test added before or after its SUT?); (d) actual assertion coverage under execution.

- **Mutation testing recommendation:** Run **[Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/)** against `tests/Lfm.Api.Tests` and `tests/Lfm.App.Tests`. Scope suggestion: `dotnet stryker --since main` for PR-scoped runs; a full run from each test project directory for an initial baseline. Target files to mutation-check first: [HealthFunctionTests.cs](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs), [RunsSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs) (retry logic), and the `AuthPages` components — these have the highest smell density and are the likeliest to harbor surviving mutants.

## Prioritized remediation worklist

### P0 — block-severity

- **Rewrite or delete [ToastHelperTests.cs](../../tests/Lfm.App.Tests/ToastHelperTests.cs) (2 tests).** Either delete (thin adapter doesn't warrant a test) or rewrite against a capturing `IToastService` fake and assert "a success/error toast with this message was displayed." Effort: 15 min.

### P1 — warn-severity, highest value

- **Fix [AuthPagesTests.cs](../../tests/Lfm.App.Tests/AuthPagesTests.cs) render-without-crash tests (4 tests: L13, L71, L79, L88).** Either delete them or assert on locale-keyed user-visible content per page. Effort: 30 min. Impact: restores real regression coverage for the entire unauthenticated surface.
- **Delete [GuildPagesTests.cs::GuildAdminPage_Renders_Without_Crash (L90)](../../tests/Lfm.App.Tests/GuildPagesTests.cs#L90).** The next test already asserts the title. Effort: 1 min.
- **Extract DTO test fixtures** for [InstancesListFunctionTests.cs](../../tests/Lfm.Api.Tests/InstancesListFunctionTests.cs), [SpecializationsListFunctionTests.cs](../../tests/Lfm.Api.Tests/SpecializationsListFunctionTests.cs), [WowUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/WowUpdateFunctionTests.cs). Build the expected DTO once in the Arrange section from the same data used to stub the repo, then `BeEquivalentTo` the returned object. Effort: 45 min.
- **Document the `/health/ready` error contract** and either cite it from [HealthFunctionTests.cs](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs) (L65, L79) or loosen the assertion to only check `status == "unready"`. Effort: 20 min.

### P2 — warn-severity, structural

- **Rewrite [RunsSignupFunctionTests.cs::Run_retries_on_concurrency_conflict_and_succeeds (L349)](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs#L349).** Assert on the returned `OkObjectResult` body (the retried state must be reflected) and drop `Times.Exactly(2)`. Effort: 15 min.
- **Drop redundant `Times.Exactly` verifications in [RunsSignupFunctionTests.cs::Run_returns_409_after_exhausting_concurrency_retries (L377)](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs#L377).** Effort: 5 min.
- **Replace manual `callOrder` list in [MeDeleteFunctionTests.cs (L44)](../../tests/Lfm.Api.Tests/MeDeleteFunctionTests.cs#L44)** with `MockSequence` / `InSequence`. Effort: 15 min.
- **Split or simplify [BattleNetLogoutFunctionTests.cs (L61)](../../tests/Lfm.Api.Tests/BattleNetLogoutFunctionTests.cs#L61)** cookie-deletion assertion — pick the actual SUT encoding and pin it declaratively. Effort: 15 min.
- **Move app-side hardcoded error strings** (`"Failed to load characters."`, `"Failed to load guild data."`, `"Run not found."`) into the locale JSON and assert via `Loc(...)` (affects [CharactersPagesTests.cs L87/L103](../../tests/Lfm.App.Tests/CharactersPagesTests.cs#L87), [GuildPagesTests.cs L73](../../tests/Lfm.App.Tests/GuildPagesTests.cs#L73), [RunsPagesTests.cs L195](../../tests/Lfm.App.Tests/RunsPagesTests.cs#L195)). Effort: 45 min. Side benefit: fixes a real i18n gap — Finnish users currently see English error copy.

### P3 — info, nice-to-have

- **Consolidate the nine `Run_method_has_RequireAuth_attribute` reflection tests** into a single assembly-scanning `[Theory]` in a new `FunctionAuthorizationContractTests.cs`, with an explicit allow-list for anonymous endpoints (`battlenet-login`, `health`, `specializations-list`, `instances-list`). Effort: 45 min. Net: removes 8 tests, adds 1 ratchet test that catches unprotected new endpoints automatically.
- **Add the missing mixed-explicit-vs-default edge case** in [GuildPermissionsTests.cs (L189)](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs#L189). Effort: 10 min.
- **Run Stryker.NET** against both test projects for a mutation-score baseline; prioritize surviving-mutant fixes in files flagged `adequate` or `weak`. Effort: ~2h wall-clock (mostly Stryker runtime).
