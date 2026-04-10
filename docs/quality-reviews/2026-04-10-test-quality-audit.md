# Test Quality Audit — `tests/Lfm.Api.Tests` + `tests/Lfm.App.Tests`

**Date:** 2026-04-10 (static audit) · 2026-04-10 (Stryker.NET follow-up appended)
**Mode:** deep
**Extensions loaded:** `dotnet` (xUnit + Moq + FluentAssertions + bUnit)
**Scope:** 53 unit-test files, ~239 tests
**Rubric:** [docs/quality-reference/unit-testing.md](../quality-reference/unit-testing.md) + [.claude/skills/test-quality-audit/extensions/dotnet.md](../../.claude/skills/test-quality-audit/extensions/dotnet.md)
**Mutation baseline:** Stryker.NET 4.14.1 — see [§ Mutation testing follow-up](#mutation-testing-follow-up-strykernet) at the bottom.

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
- **Run Stryker.NET** against both test projects for a mutation-score baseline; prioritize surviving-mutant fixes in files flagged `adequate` or `weak`. Effort: ~2h wall-clock (mostly Stryker runtime). **Done — see [§ Mutation testing follow-up](#mutation-testing-follow-up-strykernet) below.**

---

## Mutation testing follow-up (Stryker.NET)

**Date added:** 2026-04-10 (same day as static audit)
**Tool:** Stryker.NET 4.14.1 (installed via [`.config/dotnet-tools.json`](../../.config/dotnet-tools.json))
**API run duration:** 28 seconds
**App run:** **failed — known Blazor WASM limitation** (see [§ App project limitation](#app-project-limitation-blazor-wasm))

### What mutation testing adds to the static audit

The static audit grades **test quality** — whether each existing test is derived from a stated requirement or merely echoes current implementation. It cannot answer:

- Are there assertions the tests *execute* but don't *verify*?
- Are there files with no tests at all?
- Are there boundary cases the tests don't cover?

Stryker answers all three mechanically: it makes one small change to the production code at a time (a "mutant"), re-runs the whole test suite, and records whether any test failed. A surviving mutant means the change went unnoticed — either the tests didn't execute that code, or they executed it without verifying the behavior that changed.

Mutation score = `(Killed + Timeout) / (Killed + Survived + Timeout + NoCoverage)`.

### API baseline score: 38.60%

| Category | Count | Meaning |
|---|---|---|
| **Killed** | 441 | ✅ Tests noticed the mutation |
| **Survived** | 189 | ❌ Tests executed the code but didn't verify the behavior |
| **NoCoverage** | 514 | ❌ Tests never executed this code at all |
| Timeout | 1 | Mutation caused the suite to hang; counted as killed |
| Ignored | 178 | Syntactic no-ops (e.g. block removal in try/catch); excluded from the score |
| CompileError | 240 | Mutation produced invalid code; excluded from the score |

**Reading this score:** Industry benchmarks for mutation testing put 80%+ at "strong", 60–80% at "adequate", below 60% at "weak". 38.60% for the API project places the suite in "weak" — but the breakdown tells a more specific story:

- **189 survivors (16.5%)** — the tests reached this code but didn't check what mattered. These are the "mutation surfaced a missing assertion" findings. Many correspond to gaps the static audit already flagged.
- **514 no-coverage (44.9%)** — nearly half the mutations were in code no test ever touches. These are entire files or methods the audit couldn't see because the audit only examines files that have tests.

### The biggest finding: files entirely without tests

Stryker found **12 production files whose code is never executed by any unit test**. The static audit was blind to this class of gap because it only rates existing tests, not production code coverage.

| File | Mutants missed | Notes |
|---|---|---|
| [api/Program.cs](../../api/Program.cs) | 74 | DI / startup wiring. Typically not unit-tested — acceptable. |
| [api/Services/ReferenceSync.cs](../../api/Services/ReferenceSync.cs) | 73 | Reference-data sync worker. **Real gap.** |
| [api/Middleware/CorsMiddleware.cs](../../api/Middleware/CorsMiddleware.cs) | 46 | CORS policy enforcement. **Real gap, security-adjacent.** |
| [api/Services/SiteAdminService.cs](../../api/Services/SiteAdminService.cs) | 22 | Admin elevation lookup. **Real gap, security-adjacent.** |
| [api/Middleware/AuthPolicyMiddleware.cs](../../api/Middleware/AuthPolicyMiddleware.cs) | 22 | Enforces `[RequireAuth]`. **Real gap, the backbone of every other auth test.** |
| [api/Middleware/AuthMiddleware.cs](../../api/Middleware/AuthMiddleware.cs) | 16 | Session cookie → principal resolution. **Real gap, security-adjacent.** |
| [api/Repositories/RaidersRepository.cs](../../api/Repositories/RaidersRepository.cs) | 15 | Cosmos repository. Carve-out: repositories are process boundaries. |
| [api/Middleware/AuditMiddleware.cs](../../api/Middleware/AuditMiddleware.cs) | 14 | Audit event emission at middleware layer. **Real gap.** |
| [api/Repositories/SpecializationsRepository.cs](../../api/Repositories/SpecializationsRepository.cs) | 8 | Cosmos repository. Carve-out. |
| [api/Repositories/InstancesRepository.cs](../../api/Repositories/InstancesRepository.cs) | 8 | Cosmos repository. Carve-out. |
| [api/Repositories/GuildRepository.cs](../../api/Repositories/GuildRepository.cs) | 6 | Cosmos repository. Carve-out. |
| [api/Functions/CorsPreflightFunction.cs](../../api/Functions/CorsPreflightFunction.cs) | 1 | Trivial preflight responder. |

**The significant finding:** the entire **authentication pipeline** (`AuthMiddleware` → `AuthPolicyMiddleware` → `AuditMiddleware`) has zero direct unit test coverage. The ~9 `Run_method_has_RequireAuth_attribute` reflection tests the audit flagged are the only thing standing between your functions and an accidentally-unprotected endpoint — and they only check that the attribute is present, not that the middleware actually enforces it. Combined with [api/Services/SiteAdminService.cs](../../api/Services/SiteAdminService.cs) also being uncovered, the "is this user actually allowed to do this" code path has no behavioral tests at all.

Repositories that wrap Cosmos calls are arguably out-of-scope per the dotnet extension's carve-outs (process boundaries). But [api/Services/ReferenceSync.cs](../../api/Services/ReferenceSync.cs) and [api/Middleware/CorsMiddleware.cs](../../api/Middleware/CorsMiddleware.cs) are in-scope and untested.

### Audit vs. mutation: concordance and discordance

The most useful part of running Stryker after a static audit is seeing where the two methods agree and where they disagree.

#### Agreement — static audit correctly called these out

These files had test-quality concerns flagged in the audit *and* have matching surviving mutants. Mutation testing confirms the gaps.

| File | Audit verdict | Stryker survivors | What Stryker confirms |
|---|---|---|---|
| [api/Functions/RunsSignupFunction.cs](../../api/Functions/RunsSignupFunction.cs) | `adequate` — `HC-6`/`dotnet.HC-1` on retry-count pinning | **25 survivors** | The audit was right. 5 survivors are in the retry loop itself (L141, L150, L154, L177, L194 — retry index arithmetic, existingIndex comparisons). The retry-count pinning mentioned in the audit protects only the count, not the behavior under retry. 10 more survivors are in permission checks (`CreatorBattleNetId`, `CreatorGuildId` comparisons) the tests don't cover at the boundary. |
| [api/Functions/GuildFunction.cs](../../api/Functions/GuildFunction.cs) | `strong` (5/5 spec) but included ambiguous `dotnet.HC-6` attribute test | **11 survivors** | Audit missed this. Survivors are in the guild-update merge logic — `Setup is null` conditionals (L99), null-coalescing on `Slogan` (L119), and `RankPermissions` mapping (L111). The "happy path update" test verifies the success case but not the null-branch mergers. |
| [api/Services/GuildPermissions.cs](../../api/Services/GuildPermissions.cs) | `adequate` — noted `LC-6` edge-case gap at L189 | **13 survivors + 25 no-coverage** | Audit found one edge-case gap. Stryker found 13. The most important ones: the stale-roster threshold at three sites (L71, L126, L180) — tests use `FromHours(2)` and `FromHours(0)` but not the exact 1-hour boundary, so `>` vs `>=` can be flipped without any test failing. Also `FirstOrDefault() → First()` at L99 and L153 means there's no test for "rank has no matching permission entry", so the null-coalesce default path is uncovered. |
| [api/Functions/BattleNetCharactersFunction.cs](../../api/Functions/BattleNetCharactersFunction.cs) | `strong` (3/3 spec) but audit noted "no negative test for expired cooldown" | **13 survivors** | Audit was right about the gap. L71 boundary `elapsed.TotalMilliseconds <= AccountCharsCooldownMs` flipped — no test. L94–L103 null-coalesce mutations on the Blizzard response-mapping path — no test for partial/missing character data. |

#### Disagreement — audit too lenient, Stryker surfaced real gaps

These files were rated `strong` by the static audit but have meaningful surviving mutants. This is the most important category: it's where mutation testing *catches things the audit cannot*.

| File | Audit verdict | Stryker survivors | What mutation testing found that static audit missed |
|---|---|---|---|
| [api/Functions/RaiderCleanupFunction.cs](../../api/Functions/RaiderCleanupFunction.cs) | `strong` (5/5 spec) | **14 survivors** | All 14 are in the logging/counting summary branch: `removed++`, `errors++`, the `$", {errors} error(s)"` conditional suffix at L59, and the final log message. The audit said "excellent ordering tests" — and it was right about the ordering — but the tests never assert on the summary log output at all. If the counters were wrong or the summary suffix logic broke, no test would notice. |
| [api/Functions/BattleNetCallbackFunction.cs](../../api/Functions/BattleNetCallbackFunction.cs) | `strong` (7/7 spec) | **17 survivors** | Heavy survivor cluster in cookie setup — `CookieOptions` object initializer (L118), `HttpOnly` / `Secure` / `SameSite` boolean mutations (L120, L121, L154, L155). Tests verify "a Set-Cookie header is emitted" but not the security flags on it. Also survivors in raider upsert (L93 conditional, L100 arithmetic on TTL 180*86400). |
| [api/Middleware/RateLimitMiddleware.cs](../../api/Middleware/RateLimitMiddleware.cs) | `strong` (7/7 spec) | **16 survivors** | Forwarded-header parsing (L98–L99) is entirely uncovered — tests use direct RemoteIpAddress, never a proxy-forwarded scenario. The `count % 500` logging throttle at L67 and L67 arithmetic mutations survived (no test exercises the 500-request cadence). L57's `isAuth && WriteMethods.Contains` → `isAuth \|\| WriteMethods.Contains` mutation also survived. |
| [api/Services/BlizzardOAuthClient.cs](../../api/Services/BlizzardOAuthClient.cs) | `strong` (8/8 spec) | **5 survivors** | Real gaps despite excellent-looking tests: L113 `parts.Length <= 2` boundary flip survived (no test for exactly-2-parts payload). L120 `IsNullOrEmpty(state) \|\| IsNullOrEmpty(codeVerifier)` → `&&` flipped (no test for "one is empty, the other isn't"). L125 block removal in the catch of `UnprotectLoginState` — the catch body is never triggered by any test. |
| [api/Functions/BattleNetLoginFunction.cs](../../api/Functions/BattleNetLoginFunction.cs) | `strong` (7/7 spec) | **5 survivors** | Same cookie-flag pattern as `BattleNetCallbackFunction`: `CookieOptions` initializer, `HttpOnly`/`Secure` booleans, cookie name string. Tests cover PKCE and state generation (which are great) but not the cookie configuration. |
| [api/Auth/DataProtectionSessionCipher.cs](../../api/Auth/DataProtectionSessionCipher.cs) | `strong` (7/7 spec) | **2 survivors** | L8 `Purpose = "Lfm.Session.v1"` → `""` survived. This is subtle and worth fixing: the purpose string is a DataProtection key isolator. If two services share a provider but use different purposes, tokens from one can't decrypt the other. With this mutation undetected, someone could change the purpose (or accidentally collide with another service) without any test noticing. L25 block removal in the catch of `Unprotect` — the tamper tests exercise *input-validation* failures, but the catch block itself is shaped around a specific exception path that the tests may not exercise fully. |
| [api/Functions/RunsDetailFunction.cs](../../api/Functions/RunsDetailFunction.cs) | `strong` (3/3 spec) | **4 survivors** | L46 logical mutation on `GuildId is not null OR CreatorGuildId is not null` (permission guard), L98 equality flip on `RaiderBattleNetId != currentBattleNetId` (the `IsCurrentUser` sanitization). The sanitization is *tested* but the flipped-equality mutant survived — meaning the test's assertion on `IsCurrentUser=true` for the current user passes but wouldn't fail if the logic incorrectly marked another user as current. |
| [api/Helpers/RunEditability.cs](../../api/Helpers/RunEditability.cs) | `strong` (7/7 spec, exhaustive boundaries) | **1 survivor** | L18 `start <= now` → `start < now`. The audit called this file "exhaustively covers boundaries" — and it does for `signupCloseTime`. The test at L53 (`Returns_true_when_signup_close_time_equals_now`) covers the equality case for close-time, but there's no equivalent for `startTime`. When `startTime == now`, the original returns true and the mutant returns false — undetected. |
| [api/Functions/RunsCreateFunction.cs](../../api/Functions/RunsCreateFunction.cs) | `strong` (4/5 spec) | **11 survivors** | TTL arithmetic at L111–L112 (`startTimeMs - RunTtlAfterStartMs`, `Max → Min`, `expiryMs - createdAtMs) * 1000`) all flipped without test failures. The creation test verifies "it creates" but not the TTL math. Also null-coalesce defaults at L122–L129 for optional fields. |

#### Concordance — where static audit and mutation testing agree it's fine

These files rated `strong` by audit and have zero surviving mutants (where they were covered at all):

- [api/Functions/RunsDeleteFunction.cs](../../api/Functions/RunsDeleteFunction.cs) — 19 killed, 4 survived (2 are string mutations on error paths)
- [api/Functions/RunsUpdateFunction.cs](../../api/Functions/RunsUpdateFunction.cs) — very few mutations (most excluded as compile errors) but what ran was clean
- [api/Functions/MeUpdateFunction.cs](../../api/Functions/MeUpdateFunction.cs) — 6 killed, 0 survived
- [api/Functions/HealthFunction.cs](../../api/Functions/HealthFunction.cs) — 5 killed, 0 survived, 1 no-coverage (`Live()` has no test — see [§ Concrete example](#concrete-example-healthfunction))

### Concrete example: HealthFunction

Before the baseline run, I ran Stryker scoped to just [api/Functions/HealthFunction.cs](../../api/Functions/HealthFunction.cs) to demonstrate the method. The 9-mutation run found one gap the static audit completely missed:

**The `Live()` method — the `/api/health` liveness probe — has zero test coverage.** The existing [`Health_returns_ok_status_and_timestamp`](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs#L18) test calls the `HealthFunction.Build()` static helper directly; it never invokes `Live()` itself, so the `[HttpTrigger]` attribute, the route `/health`, and the `OkObjectResult` wrapping are all unverified. This is the endpoint App Service Health Check pings to decide whether to recycle the instance in production.

The static audit rated `HealthFunctionTests.cs` as `adequate` based on the tests it could see. It could not see that one of those tests called the wrong method.

**Fix:** add a test that invokes `Live()` directly against a constructed `HealthFunction` instance. See the scoped run output in the conversation for the recommended snippet. Expected score after fix: 100% on `HealthFunction.cs`.

### App project limitation (Blazor WASM)

Stryker.NET 4.14.1 **cannot mutate [app/](../../app/)**. Every attempt failed with:

```
WRN An unidentified mutation in app/Program.cs resulted in a compile error
    CS8805: Program using top-level statements must be an executable.
WRN An unidentified mutation in app/Program.cs resulted in a compile error
    CS0246: The type or namespace name 'App' could not be found
FTL Stryker.NET could not compile the project after mutation.
```

The root cause: Blazor WASM's `Program.cs` references the `App` type, which is generated at build time by the Razor source generator from `App.razor`. Stryker runs its own Roslyn compilation step for each mutation, and that step does not invoke the Razor source generators, so `App` is unresolvable. `--mutate` flags with positive or negative patterns do not help — Stryker still compiles the whole project as a baseline.

**Workarounds attempted (none worked):**
- `--mutate "!**/Program.cs"` — exclusion pattern did not prevent Stryker's compile step from processing `Program.cs`.
- `--mutate "**/Services/**/*.cs" --mutate "**/i18n/**/*.cs"` — positive scoping also did not skip the project-level compile.

**Practical options going forward:**
1. **Accept the limitation.** Static audit (already completed) remains the quality signal for [app/](../../app/). This is the pragmatic choice.
2. **Extract pure-C# logic to a separate library** (e.g. `Lfm.App.Core`) that Stryker can mutate independently. Services like `RunsClient`, `ThemeService`, `LocaleService`, `JsonStringLocalizer` are prime candidates — they have no Razor dependencies. This is a project-structure change, not a test change; budget a half-day.
3. **Wait for Stryker.NET Blazor support.** Tracking issue exists but no ETA. Not actionable.

The static audit already rates App-side test quality (~80 tests) as `strong` overall with the specific weak spots called out in the main report. The mutation score would add precision but is not essential for the weak spots that are already identified.

### New remediation items surfaced by mutation testing

Adding to the worklist above (items here are **in addition to** the P0–P3 already listed):

#### P1 — new findings from mutation testing

- **Add unit tests for [api/Middleware/AuthPolicyMiddleware.cs](../../api/Middleware/AuthPolicyMiddleware.cs)** and [api/Middleware/AuthMiddleware.cs](../../api/Middleware/AuthMiddleware.cs). The entire request-authorization pipeline is untested at the middleware layer. Current tests only verify that function methods *carry* the `[RequireAuth]` attribute via reflection — no test verifies that the attribute is actually *enforced*. Budget: 2h.
- **Add a `Live_returns_ok` test to [tests/Lfm.Api.Tests/HealthFunctionTests.cs](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs)** that invokes the `Live()` method directly. Effort: 5 min. (Raises `HealthFunction.cs` mutation score from 83% → 100%.)
- **Add cookie-flag assertions to [tests/Lfm.Api.Tests/BattleNetCallbackFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCallbackFunctionTests.cs) and [BattleNetLoginFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetLoginFunctionTests.cs).** Currently tests check "a Set-Cookie exists" — add assertions that verify `HttpOnly`, `Secure`, `SameSite=Lax`, and the cookie name/path. This kills ~10 surviving mutants across the two files and locks down a real security surface. Effort: 30 min.
- **Add tests for [api/Services/ReferenceSync.cs](../../api/Services/ReferenceSync.cs)** — the background sync service. Currently 73 mutations, 0 coverage. Budget: 1.5h.
- **Add boundary test for `startTime == now`** in [tests/Lfm.Api.Tests/RunEditabilityTests.cs](../../tests/Lfm.Api.Tests/RunEditabilityTests.cs) — mirrors the existing `signup_close_time_equals_now` test. Effort: 5 min.
- **Add a `Purpose` round-trip test** for [api/Auth/DataProtectionSessionCipher.cs](../../api/Auth/DataProtectionSessionCipher.cs) — verify that two cipher instances created from the same provider but with different `Purpose` strings cannot decrypt each other's tokens. Effort: 10 min.

#### P2 — new findings from mutation testing

- **Add tests for [api/Services/SiteAdminService.cs](../../api/Services/SiteAdminService.cs)** (22 mutations, 0 coverage). Admin lookup is security-adjacent. Budget: 45 min.
- **Add tests for [api/Middleware/CorsMiddleware.cs](../../api/Middleware/CorsMiddleware.cs)** (46 mutations, 0 coverage) and [api/Middleware/AuditMiddleware.cs](../../api/Middleware/AuditMiddleware.cs) (14 mutations, 0 coverage). Budget: 1.5h combined.
- **Add forwarded-header parsing tests to [tests/Lfm.Api.Tests/Middleware/RateLimitMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/RateLimitMiddlewareTests.cs)** — cover the `X-Forwarded-For` path at [api/Middleware/RateLimitMiddleware.cs#L98-L99](../../api/Middleware/RateLimitMiddleware.cs#L98). Effort: 20 min.
- **Add stale-roster boundary tests** at the 1-hour threshold (`fetchedAt == UtcNow - FromHours(1)`) in [tests/Lfm.Api.Tests/GuildPermissionsTests.cs](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs). Three equivalent boundaries at [api/Services/GuildPermissions.cs](../../api/Services/GuildPermissions.cs) L71, L126, L180. Effort: 15 min.
- **Add "rank with no explicit permission entry" tests** for [api/Services/GuildPermissions.cs](../../api/Services/GuildPermissions.cs) — covers the `FirstOrDefault → First` mutants at L99 and L153. Effort: 15 min.
- **Add summary log assertions to [tests/Lfm.Api.Tests/RaiderCleanupFunctionTests.cs](../../tests/Lfm.Api.Tests/RaiderCleanupFunctionTests.cs)** — verify the `removed={N}, errors={M}` counters and the conditional error-suffix appear in a captured log entry via `TestLogger<T>`. Kills all 14 surviving mutants in that file. Effort: 30 min.

#### P3 — new findings from mutation testing

- **Extract pure-C# logic from [app/](../../app/) into `Lfm.App.Core` library** so Stryker can mutate app-side code. Only worth doing if app-side quality signal becomes important. Effort: ~4h.
- **Re-run Stryker after each P1/P2 batch** to confirm targeted mutants are now killed. Expected API score after the P1 fixes: ~50–55%. After P1+P2: ~65–70%. Getting past 80% would require tackling the uncovered repositories and middleware pipeline more aggressively.

### How to reproduce

```bash
# From repo root, after dotnet tool restore
cd tests/Lfm.Api.Tests
dotnet stryker --reporter json --reporter cleartext --reporter html
```

Reports land in `tests/Lfm.Api.Tests/StrykerOutput/<timestamp>/reports/`. Open `mutation-report.html` in a browser to see surviving mutants highlighted inline in the source.

For fast PR-scoped runs after making changes:

```bash
dotnet stryker --since main --reporter html
```

For a single-file targeted run (much faster — seconds, not minutes):

```bash
dotnet stryker --mutate "**/<FileName>.cs" --reporter cleartext
```
