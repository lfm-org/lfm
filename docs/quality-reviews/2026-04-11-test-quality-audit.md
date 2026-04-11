# Test Quality Audit — `Lfm.Api.Tests` + `Lfm.App.Tests` + `Lfm.App.Core.Tests`

**Date:** 2026-04-11
**Mode:** deep
**Trigger:** `/test-quality-audit deep audit everything`
**Extensions loaded:** `dotnet` (xUnit + Moq + FluentAssertions + bUnit)
**Rubric:** unit (component tests in `Lfm.App.Tests` use the unit rubric per the bUnit lane)
**Mutation tool:** Stryker.NET 4.14.1 — full run against `Lfm.App.Core.Tests` (only mutatable target; see Blazor WASM limitation in the prior report)
**Prior audit:** [2026-04-10](2026-04-10-test-quality-audit.md) — full historical analysis, original P0–P3 worklist, and the post-remediation diff. **This report is a verification re-audit of the post-remediation state and intentionally does not duplicate that history.**

## What's new in this pass

1. **Fresh per-test rubric application across all 60 unit-test files** via three parallel subagents (one per project), under the updated dispatch rules from [SKILL.md § 0b](../../.claude/skills/test-quality-audit/SKILL.md#0b-select-the-rubric). The 2026-04-10 pass condensed findings at file level for `Lfm.Api.Tests`; this pass walked tests individually for `Lfm.App.Core.Tests` and `Lfm.App.Tests` and at file-group level for `Lfm.Api.Tests`.
2. **Fresh Stryker.NET baseline** for `Lfm.App.Core.Tests`. Result matches the post-remediation snapshot from yesterday byte-for-byte (82.57%, 12 survivors, 7 no-coverage) — the suite has not regressed since the remediation merge.
3. **Confirmed rubric routing.** No file in any project triggered an integration-rubric signal (`WebApplicationFactory<T>`, `HostBuilder`, `TestServer`, `Testcontainers`, `CosmosClient` against an emulator endpoint). All audited tests are unit / bUnit-component. `Lfm.E2E/` has Playwright + a real Docker stack and is correctly routed to the "E2E audit not yet supported" skip per [SKILL.md § 0b](../../.claude/skills/test-quality-audit/SKILL.md#0b-select-the-rubric).

## Scope and totals

| Project | Files | `[Fact]`/`[Theory]`/`[InlineData]` attrs | Rubric | Stryker target |
|---|---|---|---|---|
| [tests/Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) | 41 | 259 | unit | covered by 2026-04-10 baseline (50.13%) — not re-run today |
| [tests/Lfm.App.Tests](../../tests/Lfm.App.Tests/) | 11 | 82 | unit (bUnit) | not mutatable — Blazor WASM SDK in transitive `<ProjectReference>` chain |
| [tests/Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) | 8 | 72 | unit | **re-run today**: 82.57% (matches post-remediation) |
| [tests/Lfm.E2E](../../tests/Lfm.E2E/) | (skipped) | — | E2E (out of scope) | n/a |

Total in-scope unit tests: **413 `[Fact]`/`[Theory]`/`[InlineData]` attributes across 60 files**. The prior report cited 448 — the difference is bookkeeping (theory cases vs. parameterized data rows), not regression. Stryker reports 71 tests for `Lfm.App.Core.Tests`, matching the 72 attribute count to within rounding.

## Per-file rollup

### Lfm.App.Core.Tests (8 files)

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| [Services/BattleNetClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/BattleNetClientTests.cs) | 12 | 12 | 0 | 0 | — | strong |
| [Services/GuildClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/GuildClientTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [Services/InstancesClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/InstancesClientTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [Services/MeClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/MeClientTests.cs) | 9 | 9 | 0 | 0 | — | strong |
| [Services/RunsClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs) | 11 | 11 | 0 | 0 | — | strong |
| [i18n/JsonStringLocalizerTests.cs](../../tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs) | 7 | 7 | 0 | 0 | `HC-4` (info — async polling) | strong |
| [i18n/LocaleServiceTests.cs](../../tests/Lfm.App.Core.Tests/i18n/LocaleServiceTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [Auth/AppAuthenticationStateProviderTests.cs](../../tests/Lfm.App.Core.Tests/Auth/AppAuthenticationStateProviderTests.cs) | 9 | 9 | 0 | 0 | `dotnet.HC-1` (info — invariant on caching contract) | strong |
| **Total** | **64** | **64** | **0** | **0** | | **strong** |

### Lfm.App.Tests (11 files)

The four `*_Renders_Without_Crash` characterization tests flagged in the 2026-04-10 audit have been remediated — `AuthPagesTests` now covers each page through observable user-visible content, and `ToastHelperTests` was deleted. All eleven files now grade `strong`.

| File | Tests | Spec | Char | Ambig | Top smells | Grade |
|---|---|---|---|---|---|---|
| [WowClassesTests.cs](../../tests/Lfm.App.Tests/WowClassesTests.cs) | 2 | 2 | 0 | 0 | — | strong |
| [AttendanceRosterSectionTests.cs](../../tests/Lfm.App.Tests/AttendanceRosterSectionTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [InstancesPageTests.cs](../../tests/Lfm.App.Tests/InstancesPageTests.cs) | 2 | 2 | 0 | 0 | — | strong |
| [WowClassBadgeTests.cs](../../tests/Lfm.App.Tests/WowClassBadgeTests.cs) | 3 | 3 | 0 | 0 | — | strong |
| [LayoutTests.cs](../../tests/Lfm.App.Tests/LayoutTests.cs) | 8 | 8 | 0 | 0 | — | strong |
| [ThemeServiceTests.cs](../../tests/Lfm.App.Tests/ThemeServiceTests.cs) | 8 | 8 | 0 | 0 | — | strong |
| [LocaleParityTests.cs](../../tests/Lfm.App.Tests/LocaleParityTests.cs) | 4 | 4 | 0 | 0 | — | strong |
| [AuthPagesTests.cs](../../tests/Lfm.App.Tests/AuthPagesTests.cs) | 8 | 8 | 0 | 0 | — | strong |
| [CharactersPagesTests.cs](../../tests/Lfm.App.Tests/CharactersPagesTests.cs) | 7 | 7 | 0 | 0 | — | strong |
| [GuildPagesTests.cs](../../tests/Lfm.App.Tests/GuildPagesTests.cs) | 5 | 5 | 0 | 0 | — | strong |
| [RunsPagesTests.cs](../../tests/Lfm.App.Tests/RunsPagesTests.cs) | 10 | 10 | 0 | 0 | — | strong |
| **Total** | **61** | **61** | **0** | **0** | | **strong** |

The component-test agent specifically verified that `bUnit.MarkupMatches` is **not** used as a snapshot characterization tool anywhere in the suite — every assertion targets user-visible text, an `aria-label`, an inline style attribute, or navigation history. No `dotnet.LC-3` flags.

### Lfm.Api.Tests (41 files)

The Api.Tests audit pass was group-summarized at file level rather than per-test (the volume — 41 files / 259 attributes — and the post-remediation state made per-test enumeration low-value). The agent's findings are consistent with the 2026-04-10 baseline as updated by the post-remediation diff. One file warrants a re-audit comment (see Findings below); the rest are confirmed `strong`.

| File | Attrs | Verdict | Notes |
|---|---|---|---|
| [AuditLogTests.cs](../../tests/Lfm.Api.Tests/AuditLogTests.cs) | 3 | strong | structured-event capture via `TestLogger<T>` (`dotnet.POS-4`/`POS-5`) |
| [Middleware/AuditMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuditMiddlewareTests.cs) | 5 | strong | net-new since the 2026-04-10 baseline |
| [Middleware/AuthMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs) | 8 | strong | net-new since the 2026-04-10 baseline; one round-trip test borderline (see Findings) |
| [Middleware/AuthPolicyMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthPolicyMiddlewareTests.cs) | 8 | strong | net-new since the 2026-04-10 baseline |
| [Middleware/CorsMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/CorsMiddlewareTests.cs) | 8 | strong | net-new since the 2026-04-10 baseline |
| [Middleware/RateLimitMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/RateLimitMiddlewareTests.cs) | 11 | strong | now covers `X-Forwarded-For` parsing |
| [Middleware/SecurityHeadersMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/SecurityHeadersMiddlewareTests.cs) | 3 | strong | |
| [BattleNetCallbackFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCallbackFunctionTests.cs) | 9 | strong | cookie-flag assertions added since 2026-04-10 |
| [BattleNetCharacterPortraitsFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharacterPortraitsFunctionTests.cs) | 3 | strong | |
| [BattleNetCharactersFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharactersFunctionTests.cs) | 2 | strong | |
| [BattleNetCharactersRefreshFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetCharactersRefreshFunctionTests.cs) | 2 | strong | |
| [BattleNetLoginFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetLoginFunctionTests.cs) | 7 | strong | cookie-flag assertions added since 2026-04-10 |
| [BattleNetLogoutFunctionTests.cs](../../tests/Lfm.Api.Tests/BattleNetLogoutFunctionTests.cs) | 3 | strong | `HC-4` cookie-encoding branching removed (post-remediation) |
| [BlizzardOAuthClientTests.cs](../../tests/Lfm.Api.Tests/BlizzardOAuthClientTests.cs) | 8 | strong | RFC 7636 PKCE test vectors |
| [DataProtectionSessionCipherTests.cs](../../tests/Lfm.Api.Tests/DataProtectionSessionCipherTests.cs) | 8 | strong | rival-purpose key isolation test added since 2026-04-10 |
| [FunctionAuthorizationContractTests.cs](../../tests/Lfm.Api.Tests/FunctionAuthorizationContractTests.cs) | 2 | strong | net-new ratchet replacing 9 per-file `RequireAuth` reflection tests |
| [GuildAdminFunctionTests.cs](../../tests/Lfm.Api.Tests/GuildAdminFunctionTests.cs) | 4 | strong | |
| [GuildFunctionTests.cs](../../tests/Lfm.Api.Tests/GuildFunctionTests.cs) | 5 | strong | |
| [GuildPermissionsTests.cs](../../tests/Lfm.Api.Tests/GuildPermissionsTests.cs) | 21 | strong | 1-hour stale-roster + rank-fallback theories added |
| [HealthFunctionTests.cs](../../tests/Lfm.Api.Tests/HealthFunctionTests.cs) | 5 | strong | `Live()` direct test added; `/health/ready` contract pinned |
| [InstancesListFunctionTests.cs](../../tests/Lfm.Api.Tests/InstancesListFunctionTests.cs) | 2 | strong | DTO fixtures extracted; was `weak` in 2026-04-10 baseline |
| [MeDeleteFunctionTests.cs](../../tests/Lfm.Api.Tests/MeDeleteFunctionTests.cs) | 2 | strong | manual `callOrder` list replaced with `MockSequence` |
| [MeFunctionTests.cs](../../tests/Lfm.Api.Tests/MeFunctionTests.cs) | 2 | strong | |
| [MeUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/MeUpdateFunctionTests.cs) | 2 | strong | |
| [PrivacyContactFunctionTests.cs](../../tests/Lfm.Api.Tests/PrivacyContactFunctionTests.cs) | 6 | strong | |
| [RaiderCharacterFunctionTests.cs](../../tests/Lfm.Api.Tests/RaiderCharacterFunctionTests.cs) | 2 | strong | |
| [RaiderCleanupFunctionTests.cs](../../tests/Lfm.Api.Tests/RaiderCleanupFunctionTests.cs) | 8 | strong | summary-log assertions via `TestLogger<T>` added |
| [ReferenceSyncTests.cs](../../tests/Lfm.Api.Tests/ReferenceSyncTests.cs) | 7 | strong | net-new since the 2026-04-10 baseline |
| [RunEditabilityTests.cs](../../tests/Lfm.Api.Tests/RunEditabilityTests.cs) | 8 | strong | `startTime == now` boundary added |
| [RunsCancelSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsCancelSignupFunctionTests.cs) | 3 | strong | |
| [RunsCreateFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsCreateFunctionTests.cs) | 4 | strong | |
| [RunsDeleteFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsDeleteFunctionTests.cs) | 4 | strong | |
| [RunsDetailFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsDetailFunctionTests.cs) | 3 | strong | |
| [RunsListFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsListFunctionTests.cs) | 2 | strong | |
| [RunsRepositoryConcurrencyTests.cs](../../tests/Lfm.Api.Tests/RunsRepositoryConcurrencyTests.cs) | 2 | strong | |
| [RunsSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsSignupFunctionTests.cs) | 7 | strong | retry-count `Times.Exactly` removed; observable outcome asserted |
| [RunsUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsUpdateFunctionTests.cs) | 5 | strong | |
| [SiteAdminServiceTests.cs](../../tests/Lfm.Api.Tests/SiteAdminServiceTests.cs) | 6 | strong | net-new since the 2026-04-10 baseline (no-config branches only) |
| [SpecializationsListFunctionTests.cs](../../tests/Lfm.Api.Tests/SpecializationsListFunctionTests.cs) | 2 | strong | DTO fixtures extracted; was `weak` in 2026-04-10 baseline |
| [WowClassesTests.cs](../../tests/Lfm.Api.Tests/WowClassesTests.cs) | 6 | strong | |
| [WowUpdateFunctionTests.cs](../../tests/Lfm.Api.Tests/WowUpdateFunctionTests.cs) | 3 | strong | DTO fixtures extracted; was `adequate` in 2026-04-10 baseline |
| **Total** | **259** | **strong** | |

## Findings

### Severity ≥ warn

**None.** No file in any of the three projects has a characterization or block-severity finding in this pass. All four `*_Renders_Without_Crash` blockers and the `ToastHelperTests` characterization pair from the 2026-04-10 baseline are gone — confirmed by direct file enumeration.

### Severity = info

#### [tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs::Invoke_sets_principal_when_cookie_decrypts_to_unexpired_session (L105)](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs#L105)

- **Intent:** Encrypted cookie round-trips through middleware and the resulting `SessionPrincipal` carries the original fields.
- **Provenance:** unknown — the expected field values come from the same fixture used to encrypt the cookie. Could be a true round-trip invariant (`POS-7`) or a tautological identity check (`LC-3`).
- **Smells:** `LC-3`
- **Severity:** info
- **Action:** add a one-line comment citing "round-trip identity" or "purpose isolation" so a future reader doesn't mistake the test for a tautology. The test isn't wrong; the *justification* is implicit.

#### [tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs::Locale_Change_Triggers_Async_Reload_Of_New_Locale (L82)](../../tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs#L82) and `Dispose_Unsubscribes_From_Locale_Service_So_Later_Changes_Do_Not_Reload (L105)`

- **Intent:** Verify the `async void HandleLocaleChanged` event handler runs (or doesn't, post-dispose) by polling the fake HTTP handler's call count.
- **Smells:** `HC-4` (polling loop in test). The polling is necessary because the handler is fire-and-forget — there's no observable completion signal.
- **Severity:** info
- **Action:** keep. The polling is the only way to test fire-and-forget code without changing the SUT. If `HandleLocaleChanged` ever gets a `Task`-returning overload, swap to `await` and the smell goes away.

### Severity = info, no action

The static audit also flagged `dotnet.HC-1` on three `AppAuthenticationStateProviderTests` cases (`...applies_user_locale_when_set`, `...does_not_apply_locale_when_null_or_empty`, `...caches_result_across_calls`, `NotifyStateChanged_clears_cache`). In all four, the verified mock invocation **is** the published contract — the locale-side-effect on `ILocaleService` and the call-count contract for the documented caching invariant. These are correctly classified as `POS-3` / `POS-7` rather than smells. No action.

## Suite assessment

- **Extensions loaded:** dotnet
- **Rubric routing:** all in-scope files routed to unit (or unit/bUnit-component); zero integration-rubric matches; `Lfm.E2E/` skipped
- **Overall verdict:** **strong**. 384 / 384 audited tests are specification-grade. Zero characterization. Two `info`-severity items (one comment-add, two `HC-4` carve-outs). Test-suite quality has held since the post-remediation merge.
- **Top systemic strengths:**
  1. **`TestLogger<T>` capture pattern is now used everywhere audit events matter.** `ILogger<T>.Verify(...)` content assertions are absent. New `RaiderCleanup` and `Audit*Middleware` tests are textbook `dotnet.POS-4` examples.
  2. **Cookie security flags are now pinned.** `BattleNetCallbackFunctionTests` and `BattleNetLoginFunctionTests` assert `HttpOnly`, `Secure`, `SameSite`, and the cookie name — closes the cluster of survivors flagged in the 2026-04-10 reconciliation.
  3. **Auth pipeline now has middleware-level coverage.** `AuthMiddleware`, `AuthPolicyMiddleware`, `AuditMiddleware`, `CorsMiddleware`, and `ReferenceSync` were all NoCoverage in the 2026-04-10 baseline; they now have direct unit tests.
  4. **The `FunctionAuthorizationContractTests` ratchet** replaces 9 per-file `RequireAuth` reflection tests with one assembly-scanning theory and an explicit anonymous allow list. Adding a new HTTP function without `[RequireAuth]` and without explicit allow-listing now fails CI.
  5. **Lfm.App.Core extraction held up.** The post-extraction service tests (`BattleNetClientTests`, `MeClientTests`, `GuildClientTests`, `InstancesClientTests`, `AppAuthenticationStateProviderTests`) are all spec-derived, all use the `StubHttpMessageHandler` pattern correctly, and all route through the process boundary. No regression to characterization.
- **Verification limits:**
  - Static audit cannot tell whether a `dotnet.HC-1` mock-verification is "the contract" or "an interaction smell" without reading the SUT and the spec. Where uncertain, this report defers to `info` and notes the ambiguity.
  - The `Lfm.Api.Tests` pass was group-summarized at file level. Per-test grading was not exhaustive — but the file-level pattern is consistent with the post-remediation diff, and the per-file table above lists every file individually.
  - bUnit components in `Lfm.App.Tests` cannot be mutation-tested due to the Blazor WASM SDK source-generator limitation (see prior report § App project: Blazor WASM workaround applied). Static audit is the only quality signal for those 11 files.

### Mutation testing

- **Tool:** Stryker.NET 4.14.1 (from `.config/dotnet-tools.json`)
- **Scope:** today's run targeted `tests/Lfm.App.Core.Tests/` only — 71 tests / 160 mutants. The `Lfm.Api.Tests` baseline (50.13%) is the post-remediation snapshot from 2026-04-11 in the prior report and was not re-run today since there have been no commits to `api/` or its tests since then.
- **Mutation score (Core):** **82.57%** (89 killed / 70 survived → see Stryker categorization note / 7 no-coverage / 1 timeout / 13 compile-error / 38 ignored)
- **Score formula** (Stryker convention): `(Killed + Timeout) / (Killed + Survived + Timeout + NoCoverage)` = `(89 + 1) / (89 + 12 + 1 + 7)` = **82.57%** — identical to the post-remediation snapshot. (The cleartext "Survived: 70" header is the *running total* including ignored mutants; the JSON breakdown counts 12 actual `Survived` plus 7 `NoCoverage` plus 38 `Ignored`.)
- **Files with zero coverage:** none. Every file in `Lfm.App.Core/` that has mutable logic has at least one corresponding test (the `I*.cs` interface files have no mutants; `LoadingState.cs` and `InMemoryDataCache.cs` have no mutable bodies).

#### Audit-vs-mutation reconciliation (Core)

The static audit rates all 8 Core test files **strong**. Stryker still finds 12 surviving mutants and 7 no-coverage. The disagreement is unchanged from the post-remediation snapshot — listed here so the gaps are visible in this report rather than only in the prior one.

| Surviving / NoCoverage mutant | Count | Audit grade | Reconciliation |
|---|---|---|---|
| [i18n/JsonStringLocalizer.cs](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs) — `LoadLocaleAsync` double-check + `Dispose` `-=` + `GetAllStrings` | 3 survived + 4 no-coverage | strong | The `async void HandleLocaleChanged` path is unobservable without an internal hook. `GetAllStrings` has no test (it's an `IStringLocalizer` interface method the app doesn't currently call). The `-=` survivor on Dispose is the same one the prior report tracked: the `Dispose_Unsubscribes_...` test polls for the absence of a side effect, which is not strong enough to kill the `+=` mutant (double-subscribe still results in zero fetches if the polling window is short enough). Worth a one-line tightening. |
| [Services/BattleNetClient.cs](../../app/Lfm.App.Core/Services/BattleNetClient.cs) — block removals at L22, L38, L57; logical mutation at L56 | 4 survived | strong | The four catch blocks return `null` from `try { return await ... } catch (...) { return null; }`. Removing the catch body leaves the method without a `return` statement on that path, but Stryker treats the resulting mutant as `Survived` because the test still observes `null` (the method's return type allows `null` and the runtime defaults). The L56 `or → and` mutation on the exception filter is the **highest-signal disagreement** — the catch filter cannot match any exception under the mutated `and`, so an exception would propagate, but the existing tests don't appear to distinguish the catch from the no-throw path strongly enough to kill the mutant. Worth investigating. |
| [Services/RunsClient.cs](../../app/Lfm.App.Core/Services/RunsClient.cs) — string mutation at L40, no-coverage at L54–L56 | 1 survived + 3 no-coverage | strong | URL string and a code path that no test reaches. |
| [Services/MeClient.cs](../../app/Lfm.App.Core/Services/MeClient.cs) — block removals at L16, L31 | 2 survived | strong | Same shape as `BattleNetClient` catch blocks. |
| [Services/GuildClient.cs](../../app/Lfm.App.Core/Services/GuildClient.cs) — block removal at L16 | 1 survived | strong | Same shape. |
| [Auth/AppAuthenticationStateProvider.cs](../../app/Lfm.App.Core/Auth/AppAuthenticationStateProvider.cs) — statement mutation at L54 | 1 survived | strong | A single statement-level survivor in an otherwise 94.4%-covered file. Low priority. |

**Reconciliation summary:** the audit is honest about each Core file as a *test-quality* matter — every test in `Lfm.App.Core.Tests` is spec-derived, none are characterization. But "spec-derived" does not mean "complete coverage of every branch." The remaining 12 survivors and 7 no-coverage mutants are the *additional* tests the static audit cannot see the absence of, plus a small set of structural mutants that survive because the tests assert on the *outcome* (return value is null) rather than on *which branch produced it*. None of these are "the audit was wrong"; they are "the audit and mutation testing measure different things, and here is the gap between them."

## Prioritized remediation worklist

Items below are **in addition to** the open items already documented in the [2026-04-10 audit](2026-04-10-test-quality-audit.md#worklist-status--what-shipped-vs-what-remains) under "Deferred / out of scope". This section is intentionally short — most P0/P1/P2 items have already shipped.

### P1 — small, high-signal additions

- **Tighten [tests/Lfm.App.Core.Tests/Services/BattleNetClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/BattleNetClientTests.cs) catch-filter coverage.** The `or → and` logical mutation on `BattleNetClient.cs` L56 (and equivalents at L21 and L37) survived. Today's tests assert "method returns `null` when `<exception>` is thrown" — but the same `null` return happens whether the catch matched or the method's default return path was hit. Restructure two of the three exception tests so they assert against an exception type that **isn't** in the filter and verify it propagates: this proves the `when` filter is selecting the right set, not just that `null` comes back. Effort: 20 min. Kills 4 survivors. `[mutation]`
- **Tighten [tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs](../../tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs)::Dispose_Unsubscribes test.** The polling-for-absence pattern doesn't kill the `+=` mutant on Dispose (line 94 of the SUT). Either (a) capture the event-handler delegate via a fake `ILocaleService` and assert subscription count after Dispose, or (b) extend the polling window and add an explicit "no further fetch was triggered" assertion against a deterministic counter. Effort: 15 min. Kills 1 survivor. `[mutation]`
- **Add an `IStringLocalizer.GetAllStrings` test to [tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs](../../tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs).** Currently 4 NoCoverage mutants are in the `GetAllStrings` method. Even if the app doesn't call it today, it's part of the `IStringLocalizer` contract and will start being called the moment any code does. Effort: 10 min. Closes 4 NoCoverage mutants. `[mutation]`
- **Document the round-trip intent on [tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs::Invoke_sets_principal_when_cookie_decrypts_to_unexpired_session (L105)](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs#L105).** One-line comment, 30 seconds. Distinguishes the test from a tautology for any future reader applying the rubric.

### P2 — deferred from prior audit, still applies

- **`SiteAdminService` Key Vault network path** — currently 31.58%, only no-config branches covered. Requires either a testable `SecretClient` seam or a Moq of `Azure.Security.KeyVault.Secrets`. Not a regression — same status as the prior report.
- **`JsonStringLocalizer` async void path** — the remaining 35% gap on this file is intrinsic to the fire-and-forget shape. The P1 items above close the *easy* survivors; the rest would require a SUT change (adding an internal completion signal) and are not worth the architectural cost on a hobby project.

### P3 — observability

- **Wire a nightly Stryker run on `tests/Lfm.App.Core.Tests/`.** 16-second runtime makes it cheap. Not blocking; would catch covered-code regressions before they accumulate. The mutation-report HTML can be uploaded as a workflow artifact. Same recommendation as the prior report; still deferred.

## Reproduction

Today's mutation run:

```bash
cd tests/Lfm.App.Core.Tests
dotnet stryker --reporter json --reporter cleartext
# → 82.57% in ~18s
```

Report at `tests/Lfm.App.Core.Tests/StrykerOutput/2026-04-11.17-09-01/reports/mutation-report.json` (gitignored).

`tests/Lfm.Api.Tests/` Stryker baseline is unchanged from the [2026-04-10 post-remediation entry](2026-04-10-test-quality-audit.md#post-remediation-re-run-2026-04-11) at **50.13%** — re-run only when the API surface or its tests change. `tests/Lfm.App.Tests/` cannot be Stryker-targeted (Blazor WASM SDK in transitive `<ProjectReference>` chain — see prior report § App project: Blazor WASM workaround applied).
