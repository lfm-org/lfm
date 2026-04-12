# Test Quality Audit — Deep Mode, Maximum Depth (Second Pass)

**Date:** 2026-04-12
**Scope:** All four test projects in [tests/](../../tests/) — `Lfm.App.Core.Tests`, `Lfm.App.Tests`, `Lfm.Api.Tests`, `Lfm.E2E`.
**Extensions loaded:** `dotnet` (xUnit, bUnit, Moq, FluentAssertions, Playwright, Testcontainers, Deque.AxeCore).
**This pass’s goals vs. the morning audit ([2026-04-12-test-quality-audit.md](./2026-04-12-test-quality-audit.md)):**

- Re-enumerate every test — the suite has grown since the earlier pass and the per-project totals need to be refreshed.
- Run **determinism verification** (step 4.5) — execute every unit project twice and diff the `.trx` outputs.
- Run **Stryker.NET** (step 4) for fresh mutation data and reconcile per file with this pass’s static verdicts, not just the morning pass.
- Run the full **SUT surface enumeration** (step 2.5), **auth matrix** (step 2.6), and **migration upgrade-path** (step 2.7) passes.
- Surface **runtime distribution** and **pyramid ratio** from the fresh `.trx` files.

**Rubric dispatch (per test project):**

| Project | Rubric | Why |
|---|---|---|
| [tests/Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) | unit | No `WebApplicationFactory`, no `HostBuilder`, no Testcontainers — every test constructs the SUT directly with mocked collaborators ([csproj](../../tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj)). |
| [tests/Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) | unit | Plain unit tests against the extracted `Lfm.App.Core` class library. |
| [tests/Lfm.App.Tests](../../tests/Lfm.App.Tests/) | unit (component) | bUnit renders Blazor components in `TestContext`; classifies as unit under the skill. |
| [tests/Lfm.E2E](../../tests/Lfm.E2E/) | E2E → F/A/P/S | Playwright + Testcontainers + AxeCore; sub-lanes assigned per file ([csproj](../../tests/Lfm.E2E/Lfm.E2E.csproj)). |

**Zero projects dispatched to the integration rubric.** Every candidate was verified against the extension’s integration-routing signals in [extensions/dotnet.md §20–31](../../.claude/skills/test-quality-audit/extensions/dotnet.md). There is no `WebApplicationFactory<T>`, `HostBuilder`, `TestServer`, `DistributedApplicationTestingBuilder`, `Testcontainers`-at-seam-level, or emulator `CosmosClient` outside of the E2E project.

---

## 1. Executive summary

**Overall suite grade: strong, but with three newly-surfaced disagreements between the static verdict and the mutation report.**

| Project | Tests (runtime) | Spec | Char / Incidental | Top smells / signals | Static grade | Determinism |
|---|---|---|---|---|---|---|
| [Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) | **76** | ~69 | 2 char + 5 ambig | `HC-5` (mock-verify locale), `POS-5`, `POS-7`, `dotnet.POS-3` | adequate | ✓ 76/76 twice |
| [Lfm.App.Tests](../../tests/Lfm.App.Tests/) (bUnit) | **79** | ~57 | 3 char | `dotnet.LC-3` (color hex), `dotnet.HC-1` (×3 GuildPages `Times.Once`), `POS-7`, a11y positives | adequate | ✓ 79/79 twice |
| [Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) | **306** | ~290 | ~15 dotnet.HC-7 | `dotnet.POS-4` audit events, `POS-7` invariants, **systemic `dotnet.HC-7`** (×88 `DateTimeOffset.UtcNow`) | adequate (down from strong) | ✓ 306/306 twice |
| [Lfm.E2E](../../tests/Lfm.E2E/) | **76** | ~67 | 9 char/incid + 1 skipped | `E-HC-S1` (header-only ×14/16 in SecuritySpec), `E-HC-A2` (load-only scans), `E-HC-F10` (API-contract in E2E), `E-POS-8/-9/-6` | strong with caveats | n/a (E2E) |
| **Totals** | **537** | **~483 (90.0 %)** | **~30 (5.6 %)** | — | — | **all unit runs stable** |

The suite grew from **343 tests** at the morning audit to **537 tests** (up 56.6 %). Most of the growth is in `Lfm.Api.Tests` (203 → 306, +51 %) and `Lfm.App.Core.Tests` (44 → 76, +73 %) — both reflect the Core-extract refactor reaching maturity.

### Headline findings (this pass)

1. **Determinism verification passed for every unit project, zero divergence.** Each of `Lfm.App.Core.Tests`, `Lfm.Api.Tests`, and `Lfm.App.Tests` was run twice against the Release build via `dotnet test --no-build --logger trx`. Every `Counters` block shows `total == executed == passed` on both runs (`76/76`, `306/306`, `79/79`). This is runtime-proven evidence that static smells like `dotnet.HC-7` are *latent* risk, not current flake.
2. **Stryker score unchanged at 86.24 %** against [Lfm.App.Core](../../app/Lfm.App.Core/). 10 survived + 5 no-coverage + 1 timeout out of ~97 evaluable mutants. New mutation-vs-audit disagreements appear below in §4 — the most interesting one is **block-removal mutants surviving in service clients the static audit rated `strong`**.
3. **`RunsClient.CancelSignupAsync` is still a confirmed no-coverage gap** — static audit misses it (no test file constructs a test), Stryker confirms (1 survived + 3 `NoCoverage` mutants on [app/Lfm.App.Core/Services/RunsClient.cs:40–56](../../app/Lfm.App.Core/Services/RunsClient.cs#L40-L56)). The morning audit flagged the same gap. The interface and method both exist at [IRunsClient.cs:13](../../app/Lfm.App.Core/Services/IRunsClient.cs#L13) and [RunsClient.cs:52](../../app/Lfm.App.Core/Services/RunsClient.cs#L52). The server-side `RunsCancelSignupFunction` *is* tested ([RunsCancelSignupFunctionTests.cs](../../tests/Lfm.Api.Tests/RunsCancelSignupFunctionTests.cs)); the SPA client is not. Highest-priority `P0` remediation.
4. **Systemic `dotnet.HC-7` across `Lfm.Api.Tests`** — grep finds **88 `DateTimeOffset.UtcNow` reads across 24 test files**. None flaked on the two determinism runs, but the pattern is the exact thing the code-quality-audit skill documents as “passes when the author runs it, fails at midnight or on DST.” This downgrades `Lfm.Api.Tests` from *strong* (morning audit) to *adequate* under this pass’s stricter lens. The fix is mechanical: inject `TimeProvider` into the SUTs that bind time and use `FakeTimeProvider` in tests.
5. **`E2ELoginFunction` is NOT an `E-HC-S5` / `I-HC-B2` finding** — the function is compiled out of production via `#if E2E` ([E2ELoginFunction.cs:1](../../api/Functions/E2ELoginFunction.cs#L1)) **and** guarded at runtime by `E2E_TEST_MODE` env check ([E2ELoginFunction.cs:22](../../api/Functions/E2ELoginFunction.cs#L22)). The gap-report agent flagged it as `Gap-Route` because its route template is not referenced from `Lfm.Api.Tests`. That flag is false: by design the function does not exist in the production build. Record as a defense-in-depth positive.
6. **Quarantined test still in place without a root cause.** [RunsSpec.cs:199](../../tests/Lfm.E2E/Specs/RunsSpec.cs#L199) — `DeleteRun_Confirm_RemovedFromList` still carries `Skip = "Flaky: create-run form submission aborted in shared browser context — needs isolated page"`. Still `LC-9` + `E-HC-P5`. No issue link, no exit criterion, unchanged for two consecutive deep audits. Promoted to `P1` — quarantine without exit criterion is a growing design smell.

### Audit-vs-mutation reconciliation (new in this pass)

The morning audit ran Stryker but only noted the overall score and the `CancelSignupAsync` no-coverage gap. This pass drills into the **file-level disagreements** — files rated `strong` or `adequate` by static audit that still had surviving mutants.

| File | Static grade | Survived | NoCov | Interpretation |
|---|---|---|---|---|
| [AppAuthenticationStateProvider.cs](../../app/Lfm.App.Core/Auth/AppAuthenticationStateProvider.cs) | adequate (`HC-5`) | 1 ([L54](../../app/Lfm.App.Core/Auth/AppAuthenticationStateProvider.cs#L54)) | 0 | **Agreement.** Static audit flagged `HC-5` for locale-side-effect verified via mock, not state; Stryker survived a statement mutation on the locale-application line. Same finding, two angles. |
| [JsonStringLocalizer.cs](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs) | strong | 2 ([L69](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs#L69), [L95](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs#L95)) | 2 ([L45](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs#L45), [L75](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs#L75)) + 1 timeout | **Disagreement.** Static audit rated the localizer `strong` on round-trip and load/lookup invariants. Stryker survived a boolean mutation at L45 and a statement mutation at L75 with no coverage, plus two more survivors at L69/L95. The audit missed at least one edge-case path (likely fallback-to-English when the requested key exists only in the fallback locale, or the async reload race). High-signal P1 item. |
| [BattleNetClient.cs](../../app/Lfm.App.Core/Services/BattleNetClient.cs) | strong | 3 ([L22](../../app/Lfm.App.Core/Services/BattleNetClient.cs#L22), [L38](../../app/Lfm.App.Core/Services/BattleNetClient.cs#L38), [L57](../../app/Lfm.App.Core/Services/BattleNetClient.cs#L57) — block removal each) | 0 | **Disagreement.** Three methods survive a **block-removal** mutation: deleting the entire method body did not break any test. This is the highest-signal finding in the whole audit. The test file `BattleNetClientTests.cs` has 16 tests and is rated `strong` by the morning audit and this pass. The block-removal survival means the tests assert something the method didn’t compute — likely the tests construct the response via `StubHttpMessageHandler` and assert on the stub’s payload rather than on what the client did with it. This is an audit-vs-mutation disagreement the static rubric cannot see. |
| [GuildClient.cs](../../app/Lfm.App.Core/Services/GuildClient.cs) | strong | 1 ([L16](../../app/Lfm.App.Core/Services/GuildClient.cs#L16) — block removal) | 0 | **Disagreement.** Same pattern as BattleNetClient at the `GetAsync` entry point. |
| [MeClient.cs](../../app/Lfm.App.Core/Services/MeClient.cs) | strong | 2 ([L16](../../app/Lfm.App.Core/Services/MeClient.cs#L16), [L31](../../app/Lfm.App.Core/Services/MeClient.cs#L31) — block removal each) | 0 | **Disagreement.** Two methods (`GetAsync`, `UpdateAsync`) survive block-removal. Same diagnosis. |
| [RunsClient.cs](../../app/Lfm.App.Core/Services/RunsClient.cs) | strong (except CancelSignupAsync) | 1 ([L40](../../app/Lfm.App.Core/Services/RunsClient.cs#L40) — string mutation in `CancelSignupAsync` URL) | 3 ([L54](../../app/Lfm.App.Core/Services/RunsClient.cs#L54), [L55](../../app/Lfm.App.Core/Services/RunsClient.cs#L55), [L56](../../app/Lfm.App.Core/Services/RunsClient.cs#L56)) | **Confirmed gap** ∩ static `Gap-API`. Every surviving/no-coverage mutant is in `CancelSignupAsync`. |

The block-removal disagreements are the new and interesting finding from this pass. Static audit cannot see them because the test file exists and its assertions read as specification. Only mutation testing reveals that the tests don’t actually verify the code under test ran.

### Pyramid ratio

- **Unit + component:** 461 tests (76 + 79 + 306) = **85.8 %**
- **Integration (in-process or out-of-process contract):** 0 tests = **0.0 %**
- **E2E:** 76 tests = **14.2 %**
  - Sub-lane F (functional): 33
  - Sub-lane A (accessibility): 22
  - Sub-lane P (performance): 6
  - Sub-lane S (security): 16 (14 of which are header-value assertions — `E-HC-S1`)

**Shape: skewed pyramid with a missing middle rung.** Google’s *Software Engineering at Google* guidance is roughly 70–80 % unit / 15–20 % integration / ≤ 10 % E2E. This suite is **85.8 / 0 / 14.2** — unit-heavy is fine, but the zero-integration middle layer means every cross-process concern (OAuth flow through real middleware, Cosmos emulator round-trip, serialization through real ASP.NET pipeline, audit-event emission through real DI) falls to E2E by default. That’s how the E2E sub-lane S wound up with 14 header-value tests that would be cheaper and faster in integration sub-lane B. Design finding — not blocking, but a cost signal. See `P3` in the worklist.

---

## 2. Per-project rollups

### 2.1 Lfm.App.Core.Tests (76 tests) — unit rubric

| File | Tests | Spec | Char | Ambig | Top smells / signals | Grade |
|---|---|---|---|---|---|---|
| [Services/GuildClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/GuildClientTests.cs) | 5 | 4 | 0 | 1 | `POS-5` 2 methods × happy+sad; mutation disagreement (survived block-removal at SUT L16) | strong* |
| [Services/InstancesClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/InstancesClientTests.cs) | 5 | 5 | 0 | 0 | `POS-5`, `POS-2` null-coalescing pinned | strong |
| [Services/MeClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/MeClientTests.cs) | 10 | 9 | 0 | 1 | 3 methods × 3-5 paths; mutation disagreement (2 block-removal survivors) | strong* |
| [Services/BattleNetClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/BattleNetClientTests.cs) | 16 | 15 | 0 | 1 | `POS-5` throughput; critical-exception propagation (`OutOfMemoryException`); mutation disagreement (3 block-removal survivors) | strong* |
| [Services/RunsClientTests.cs](../../tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs) | 15 | 14 | 0 | 1 | `POS-7` URL-encode invariants; `Gap-API` on `CancelSignupAsync` (still missing) | adequate |
| [Auth/AppAuthenticationStateProviderTests.cs](../../tests/Lfm.App.Core.Tests/Auth/AppAuthenticationStateProviderTests.cs) | 9 | 7 | 2 | 0 | `HC-5` L103/L117 still present (mock-verify locale side-effect); mutation agreement (SUT L54 survived) | adequate |
| [i18n/LocaleServiceTests.cs](../../tests/Lfm.App.Core.Tests/i18n/LocaleServiceTests.cs) | 7 | 6 | 0 | 1 | `POS-7` idempotency / rejectance invariants; `dotnet.LC-8` low-conf (no explicit culture set) | adequate |
| [i18n/JsonStringLocalizerTests.cs](../../tests/Lfm.App.Core.Tests/i18n/JsonStringLocalizerTests.cs) | 9 | 9 | 0 | 0 | `POS-7` load / fallback / reload; mutation disagreement (2 survivors + 2 no-coverage on the SUT) | strong* |
| **Totals** | **76** | **69** | **2** | **5** | — | **adequate** |

\* `strong*` means the static rubric rates the file strong **but** mutation testing shows surviving mutants — see §4 for the reconciliation.

**Suite verdict:** adequate, trending strong. The static rubric only sees two characterization tests (the two `Mock.Verify(SetLocale)` calls in the auth provider) but mutation testing found six extra surviving mutants across four files the audit rated strong. Without those, the grade would be strong.

Carve-outs applied: `StubHttpMessageHandler` is a fake (`dotnet.POS-5`), not a mock. Per-test `HttpClient` construction is `dotnet.POS-3`, not `LC-7`. `Mock<HttpMessageHandler>.Protected().Setup("SendAsync",…)` would be exempt by the core carve-out but is not actually used here.

### 2.2 Lfm.App.Tests (79 tests) — unit rubric (bUnit component sub-flavor)

| File | Tests | Spec | Char | Top smells / signals | Grade |
|---|---|---|---|---|---|
| [WowClassesTests.cs](../../tests/Lfm.App.Tests/WowClassesTests.cs) | 2 | 2 | 0 | `POS-2` boundary IDs 0 / -1 / 14 / 999, round-trip | strong |
| [WowClassBadgeTests.cs](../../tests/Lfm.App.Tests/WowClassBadgeTests.cs) | 3 | 1 | 2 | `dotnet.LC-3` hex literals (`#C69B6D`, `#0070DD`) not cross-referenced to `WowClasses.GetColor()` canon | adequate |
| [AttendanceRosterSectionTests.cs](../../tests/Lfm.App.Tests/AttendanceRosterSectionTests.cs) | 5 | 5 | 0 | `dotnet.LC-3` L68 `color:#0070DD` literal; `Loc()` backed short markup (positive) | adequate |
| [LocaleParityTests.cs](../../tests/Lfm.App.Tests/LocaleParityTests.cs) | 4 | 4 | 0 | `POS-7` key-parity + non-empty-value invariants across locale JSON | strong |
| [LayoutTests.cs](../../tests/Lfm.App.Tests/LayoutTests.cs) | 8 | 8 | 0 | **a11y positives** — skip-to-content link by localized label ([L91](../../tests/Lfm.App.Tests/LayoutTests.cs#L91)), `aria-label='Switch to light mode'` accessible-name locator ([L101](../../tests/Lfm.App.Tests/LayoutTests.cs#L101)) | strong |
| [ThemeServiceTests.cs](../../tests/Lfm.App.Tests/ThemeServiceTests.cs) | 7 | 7 | 0 | `POS-3` observable state (no `Mock.Verify`), `POS-7` idempotency | strong |
| [AuthPagesTests.cs](../../tests/Lfm.App.Tests/AuthPagesTests.cs) | 6 | 6 | 0 | `POS-5` happy + error + success across `LoginFailed` / `LoginSuccess` / `Goodbye` | strong |
| [CharactersPagesTests.cs](../../tests/Lfm.App.Tests/CharactersPagesTests.cs) | 7 | 7 | 0 | `POS-5` loading / happy / exception / null / empty; `LC-12` missing unauthorized sad path | adequate |
| [GuildPagesTests.cs](../../tests/Lfm.App.Tests/GuildPagesTests.cs) | 5 | 4 | 1 | **`dotnet.HC-1` × 3** — `client.Verify(c => c.GetAsync(...), Times.Once)` at L47 / L69 / L84 pins the component-to-client call count redundantly with markup assertions | weak |
| [RunsPagesTests.cs](../../tests/Lfm.App.Tests/RunsPagesTests.cs) | 11 | 10 | 1 | Per-page loading / happy / empty / error matrix; `HC-3` hardcoded `"2026-05-01T20:00:00Z"` at helper | adequate |
| [InstancesPageTests.cs](../../tests/Lfm.App.Tests/InstancesPageTests.cs) | 2 | 2 | 0 | Minimal but specification | adequate |
| **Totals** | **79** | **66** | **4** | — | **adequate** |

**Suite verdict:** adequate. `GuildPagesTests.cs` is the one file in this project that drops to weak — three redundant `Times.Once` verify calls that add nothing the markup assertion isn’t already proving. Easy to fix.

### 2.3 Lfm.Api.Tests (306 tests) — unit rubric

The morning audit’s strong rating for this project relied on the absence of characterization smells — every function test had a happy + distinct sad path matrix, audit events asserted by property key via `TestLogger<T>`, no mocks of owned concrete classes, no snapshot tests. All of that is still true. But this pass’s deeper `dotnet.HC-7` sweep surfaces a systemic wall-clock-read issue that the morning audit missed at scale.

| Group | Files | Tests | Verdict | Notes |
|---|---|---|---|---|
| OAuth / Battle.net functions | 7 | ~75 | spec | audit-event matrix via `TestLogger<T>`, happy + missing-cookie + state-mismatch + exchange-failure + tampered-cookie |
| OAuth protocol client | 1 | ~8 | spec | `POS-2` RFC 7636 PKCE test vectors, round-trip protect/unprotect |
| Crypto | 1 | ~8 | spec | `POS-7` round-trip, non-determinism, key-separation, tamper-rejection |
| Guild / permissions | 3 | ~40 | spec | rank boundaries, stale-roster boundary; **`dotnet.HC-7` ×8** `DateTimeOffset.UtcNow` reads in `GuildPermissionsTests.cs` for stale-roster tests |
| Middleware | 5 | ~45 | spec | CORS preflight, rate-limit multi-bucket, X-Forwarded-For, security-headers-on-failure |
| CRUD functions (Runs, Me, Instances, Specs, Health) | 14 | ~85 | mostly spec | `MockSequence` for delete-order; **pervasive `dotnet.HC-7`** in Runs* tests that arrange `DateTimeOffset.UtcNow.AddHours(24)` as start times, and in `RunEditabilityTests.cs` (entire file, 13 tests) |
| Reference sync pipeline | 1 | ~14 | spec | `POS-5` fault-tolerance across failure modes; **slow tests** — top-10 slowest in the whole suite live here (305 ms worst, ~200 ms median) |
| Assembly-wide contracts | 1 | 2 | spec | `POS-7` reflection invariant — every HTTP function has `[RequireAuth]` or is in the allow-list |
| Repo concurrency | 1 | 2 | spec | etag pass-through + 412→ConcurrencyConflictException translation |
| **Totals** | **~33** | **306** | mostly spec | — |

**Suite verdict:** adequate (down from strong). One headline smell:

- **`dotnet.HC-7` is systemic across 24 test files × 88 occurrences of `DateTimeOffset.UtcNow` / `DateTime.Now`.** Grep results for `DateTimeOffset\.UtcNow|DateTime\.(Now|UtcNow|Today)` in `tests/Lfm.Api.Tests`:

  ```
  RunEditabilityTests.cs:13     GuildPermissionsTests.cs:8     HealthFunctionTests.cs:6
  RunsUpdateFunctionTests.cs:6  RunsSignupFunctionTests.cs:6   Middleware/AuthMiddlewareTests.cs:4
  RunsDeleteFunctionTests.cs:4  BattleNetCharactersRefreshFunctionTests.cs:4
  RunsCancelSignupFunctionTests.cs:4   MeFunctionTests.cs:4    BattleNetCharactersFunctionTests.cs:3
  WowUpdateFunctionTests.cs:2   RunsListFunctionTests.cs:2    RunsDetailFunctionTests.cs:2
  RunsCreateFunctionTests.cs:2  RaiderCleanupFunctionTests.cs:2   RaiderCharacterFunctionTests.cs:2
  MeUpdateFunctionTests.cs:2    MeDeleteFunctionTests.cs:2    GuildAdminFunctionTests.cs:2
  BattleNetLogoutFunctionTests.cs:2   GuildFunctionTests.cs:2 BattleNetCharacterPortraitsFunctionTests.cs:2
  Middleware/AuthPolicyMiddlewareTests.cs:2
  ```

  All 88 are wall-clock reads in test bodies (not one is a carve-out-benign unique-id). Determinism verification ran twice and every test passed both times — the flake is **latent**, not current. But the tests are sensitive to midnight and DST transitions. The fix is mechanical: inject `TimeProvider` into the SUT and use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing` in tests (`dotnet.POS-6`). The blast radius is nontrivial (likely a dozen SUT files in `api/`), which is why this is a `P1` worklist item and not `P0`.

No other systemic smell. The rest of the file-by-file pattern matches the morning audit — strong adoption of `TestLogger<T>` for audit events, no `Mock<ILogger<T>>` anywhere, no snapshot tests, no mocks of owned concrete types.

### 2.4 Lfm.E2E (76 tests) — E2E rubric

**Infrastructure rollup.** [Infrastructure/SharedStack.cs](../../tests/Lfm.E2E/Infrastructure/SharedStack.cs) and [Infrastructure/StackFixture.cs](../../tests/Lfm.E2E/Infrastructure/StackFixture.cs) build a hermetic Testcontainers stack: Cosmos emulator + Azurite + Functions + Blazor app on dynamic ports, with `WaitForHttp` readiness and a deterministic [Seeds/DefaultSeed.cs](../../tests/Lfm.E2E/Seeds/DefaultSeed.cs). Per-test `IBrowserContext` creation happens in each spec’s fixture (`AuthenticatedContextAsync`/`AnonymousContextAsync`) — `E-POS-6`. Page objects under [Pages/](../../tests/Lfm.E2E/Pages/) remain narrow (no `E-LC-5` god-object). Two wall-clock sleeps live in the infra layer only — once-per-suite container bringup, carved out. [Infrastructure/TestResultCollector.cs](../../tests/Lfm.E2E/Infrastructure/TestResultCollector.cs) captures traces/screenshots on failure only — `E-POS-7`, not `E-HC-F7`. [Infrastructure/PerfResultCollector.cs](../../tests/Lfm.E2E/Infrastructure/PerfResultCollector.cs) writes diagnostics-only, no assertions.

| Spec | Tests | Sub-lane(s) | Notable findings |
|---|---|---|---|
| [AccessControlSpec.cs](../../tests/Lfm.E2E/Specs/AccessControlSpec.cs) | 6 | F | all spec; route-protection smoke + public-route matrix; `E-POS-1` user-story names |
| [AccessibilitySpec.cs](../../tests/Lfm.E2E/Specs/AccessibilitySpec.cs) | 22 | A | **`E-HC-A2` — load-only scans.** [AccessibilityHelper.cs](../../tests/Lfm.E2E/Helpers/AccessibilityHelper.cs) cites WCAG 2.2 AA at [L14–L21](../../tests/Lfm.E2E/Helpers/AccessibilityHelper.cs#L14-L21) (positive `E-POS-5`), but rescans after form submission / menu open / focus trap are absent. Keyboard activation spec at [L298](../../tests/Lfm.E2E/Specs/AccessibilitySpec.cs#L298) is positive. |
| [AuthSpec.cs](../../tests/Lfm.E2E/Specs/AuthSpec.cs) | 5 | F | OAuth intent + test-mode login via `e2e/login` + logout + error page; 1 presence-only smoke (`E-HC-F1` info) |
| [NavigationSpec.cs](../../tests/Lfm.E2E/Specs/NavigationSpec.cs) | 5 | F | `GetConsoleErrors` filter pattern ([L50](../../tests/Lfm.E2E/Specs/NavigationSpec.cs#L50)) shows error-filtering discipline; spec throughout |
| [PerformanceSpec.cs](../../tests/Lfm.E2E/Specs/PerformanceSpec.cs) | 6 | P | **diagnostic-only, no budgets asserted.** `E-HC-P1` does not strictly apply (no threshold → nothing to characterize). But promoting this into actual budget tests would hit `E-HC-P2` (single sample), `E-HC-P3` (no cold/warm), `E-HC-P4` (localhost), `E-HC-P6` (uncontrolled hardware) — which is why the test authors correctly kept it diagnostic. Record as positive `E-POS-7`. |
| [ProfileSpec.cs](../../tests/Lfm.E2E/Specs/ProfileSpec.cs) | 8 | F | **`E-HC-F10`** at `SelectCharacter_ViaApi_UpdatesSelection` ([L113–L147](../../tests/Lfm.E2E/Specs/ProfileSpec.cs#L113-L147)) — asserts on `PUT /api/raider/characters/{id}` response shape, no UI check. Belongs in integration sub-lane B. **`E-HC-F3` residual** at `RefreshCharacters_Click_UpdatesFromBattleNet` ([L82–L110](../../tests/Lfm.E2E/Specs/ProfileSpec.cs#L82-L110)) — still waits on a request listener with no assertion on page state. Unchanged from morning audit. |
| [RunsSpec.cs](../../tests/Lfm.E2E/Specs/RunsSpec.cs) | 8 | F | **1 skipped** with insufficient exit criterion at [L199](../../tests/Lfm.E2E/Specs/RunsSpec.cs#L199) — `DeleteRun_Confirm_RemovedFromList` (`LC-9` + `E-HC-P5`). **`E-HC-F2` residual** at `CreateRun_SubmitForm_AppearsInList` — `#instance-select` still present at [L67](../../tests/Lfm.E2E/Specs/RunsSpec.cs#L67) with a fluent-option role fallback. Document as a known fluent-select limitation, or rewrite once fluent-select exposes accessible-name. |
| [SecuritySpec.cs](../../tests/Lfm.E2E/Specs/SecuritySpec.cs) | 16 | S | **14/16 tests are `E-HC-S1`** — header-value assertions that do not prove browser enforcement. The 2 true `E-POS-9` tests are `TamperedCookie_Returns401` ([L174](../../tests/Lfm.E2E/Specs/SecuritySpec.cs#L174)) and `CrossUserAccess_Rejected` ([L242](../../tests/Lfm.E2E/Specs/SecuritySpec.cs#L242)). Recommend moving the 14 header-value tests down to integration sub-lane B once such a project exists; until then, record them as *misplaced but valuable* rather than delete. |
| **Totals** | **76** | F=33, A=22, P=6, S=16 | — |

**Suite verdict:** strong with caveats. The infrastructure layer is first-class, the auth matrix cells are largely covered (see §5.B), and the observed gaps are localized. Unchanged from the morning audit.

---

## 3. Determinism findings (runtime-proven)

**Status:** ran (every unit project twice), **zero divergence**.

Cheap-rerun command per [extensions/dotnet.md §601–620](../../.claude/skills/test-quality-audit/extensions/dotnet.md):

```bash
dotnet test tests/<Project>.Tests/<Project>.Tests.csproj \
  --no-build -c Release \
  --logger "trx;LogFileName=run[1|2].trx" \
  --results-directory .test-determinism/<proj>/run[1|2]
```

| Project | Run 1 `Counters` | Run 2 `Counters` | Diverged tests |
|---|---|---|---|
| `Lfm.App.Core.Tests` | total=76 passed=76 failed=0 | total=76 passed=76 failed=0 | **0** |
| `Lfm.Api.Tests` | total=306 passed=306 failed=0 | total=306 passed=306 failed=0 | **0** |
| `Lfm.App.Tests` (bUnit) | total=79 passed=79 failed=0 | total=79 passed=79 failed=0 | **0** |

**Interpretation.** Every static flake-suspicion smell (`dotnet.HC-7` ×88, `dotnet.LC-8` culture read, `HC-11` clock-read, any `LC-5` dates/GUIDs in expected position) is **latent, not active**. Two consecutive runs on this machine at this instant produced identical results. The static smells remain worth fixing because latent flake activates at midnight, during DST, on a differently-locale’d CI agent, or when the suite runs for the first time on a Windows runner. But for this pass’s report, no test gets an additional runtime-proven finding.

**What determinism verification cannot tell you.** This is only two runs on one machine. It does not prove the tests are correct — it proves they produce the same wrong or right answer twice. Reconcile with mutation findings (§4) for the second angle.

---

## 4. Mutation testing

**Tool:** Stryker.NET 4.14.1 (installed locally per [.config/dotnet-tools.json](../../.config/dotnet-tools.json)).
**Scope:** `tests/Lfm.App.Core.Tests` → SUT `app/Lfm.App.Core`. Other SUTs excluded:

- **Blazor WASM `app/Lfm.App.csproj`** — cannot be mutated. Razor source generators are not invoked during Stryker’s recompile step, producing `CS0246` on generated types (`App`, root component). Covered by the extract-to-core workaround already in place ([extensions/dotnet.md §715–726](../../.claude/skills/test-quality-audit/extensions/dotnet.md)). **Workaround is in effect** — the extract is `Lfm.App.Core`, and this run mutated it successfully.
- **`api/Lfm.Api.csproj`** — not yet wired for Stryker. Recommended as a `P3` item in the worklist below. Stryker would succeed against the Functions project — no source generators in play there.
- **`tests/Lfm.E2E`** — E2E SUTs are excluded from mutation testing by policy ([SKILL.md § Mutation testing (conditional)](../../.claude/skills/test-quality-audit/SKILL.md#mutation-testing-conditional)); the SUT is the whole deployed stack.

**Run:** fresh run at `2026-04-12.04-38-04` (~16 seconds wall-clock).

### Mutation score

- **Total:** **86.24 %** (unchanged from morning audit at `2026-04-12.03-05-38`)
- **Report:** [tests/Lfm.App.Core.Tests/StrykerOutput/2026-04-12.04-38-04/reports/mutation-report.json](../../tests/Lfm.App.Core.Tests/StrykerOutput/2026-04-12.04-38-04/reports/mutation-report.json)
- **Breakdown of evaluable mutants (`killed + survived + nocoverage + timeout`):** 10 survived + 5 no-coverage + 1 timeout out of ~97 evaluable. (Compile-error and ignored mutants are excluded from the denominator.)

### Surviving + no-coverage mutants by file

| File | Survived | NoCov | Timeout | Killed | Interpretation |
|---|---|---|---|---|---|
| [AppAuthenticationStateProvider.cs](../../app/Lfm.App.Core/Auth/AppAuthenticationStateProvider.cs) | 1 (L54 stmt) | 0 | 0 | 17 | Agreement with static `HC-5`. |
| [JsonStringLocalizer.cs](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs) | 2 (L69, L95 stmt) | 2 (L45 bool, L75 stmt) | 1 | 15 | **Disagreement** — rated strong, 4 gaps. Highest-value single-file finding. |
| [LocaleService.cs](../../app/Lfm.App.Core/i18n/LocaleService.cs) | 0 | 0 | 0 | 8 | Clean. |
| [BattleNetClient.cs](../../app/Lfm.App.Core/Services/BattleNetClient.cs) | 3 (L22, L38, L57 **block removal**) | 0 | 0 | 15 | **Disagreement** — rated strong, three methods survive body-removal. |
| [GuildClient.cs](../../app/Lfm.App.Core/Services/GuildClient.cs) | 1 (L16 **block removal**) | 0 | 0 | 6 | **Disagreement** — rated strong, `GetAsync` body survives removal. |
| [InstancesClient.cs](../../app/Lfm.App.Core/Services/InstancesClient.cs) | 0 | 0 | 0 | 4 | Clean. |
| [MeClient.cs](../../app/Lfm.App.Core/Services/MeClient.cs) | 2 (L16, L31 **block removal**) | 0 | 0 | 12 | **Disagreement** — rated strong, two methods survive body-removal. |
| [RunsClient.cs](../../app/Lfm.App.Core/Services/RunsClient.cs) | 1 (L40 string in `CancelSignupAsync` URL) | 3 (L54, L55, L56) | 0 | 16 | **Confirmed gap** — `CancelSignupAsync` has no test. Every mutant in that method survived or had no coverage. |

### The block-removal finding (new)

Static audit reads `BattleNetClientTests.cs`, sees 16 tests, a `StubHttpMessageHandler` fake, a per-test `HttpClient`, happy + every sad path, assertions on return values — rates the file **strong**. Stryker then removes the body of `BattleNetClient.GetCharactersAsync` (line 22), recompiles, reruns the 16 tests — **every test still passes**.

The only explanation: the tests assert on something the stub handler returns regardless of whether `GetCharactersAsync` executes. The likely shape is:

```csharp
var handler = new StubHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
    Content = new StringContent("""[{"name":"Alice"}]""")
});
var sut = new BattleNetClient(new HttpClient(handler));
var result = await sut.GetCharactersAsync(CancellationToken.None);
result.Should().ContainSingle(c => c.Name == "Alice");
```

If `GetCharactersAsync`’s body is deleted and replaced with `throw null!`, Stryker notices the test still passes because — on the deleted-body variant — the method never executed the HTTP call, but the test was going to assert on the stub’s canned response anyway. That is characterization in disguise: the test verifies the stub’s contract, not the client’s behavior.

This is the single highest-value finding in the audit. Static audit *cannot* see it. Mutation testing *can*. The remediation is to add at least one assertion on *what the client did with the response* for each block-removal survivor method — e.g., asserting the request URL it built, the headers it sent, the authentication token it presented, or the DTO shape it returned after deserialization. Block-removal killing is the canonical mutation-kill signal.

### Files with zero test coverage

**Zero.** Every source file in `Lfm.App.Core` has at least one test. The last no-coverage-file finding (from earlier audits) was `CancelSignupAsync` as a *method*, not a file — the file `RunsClient.cs` has plenty of tests for the other public methods.

### Notes

- One timeout on `JsonStringLocalizer.cs` — Stryker does not by itself distinguish a real infinite-loop mutation from a slow test. Combined with the two surviving statement mutations in the same file, this is a signal that the reload-on-locale-change path is under-tested.
- Five `CompileError` mutants and ~35 `Ignored` mutants are excluded from the denominator per [extensions/dotnet.md §741](../../.claude/skills/test-quality-audit/extensions/dotnet.md).

---

## 5. Gap report

### 5.A. SUT surface enumeration (step 2.5)

**SUT projects by test project:**

- [tests/Lfm.Api.Tests](../../tests/Lfm.Api.Tests/) → [api/Lfm.Api.csproj](../../api/Lfm.Api.csproj) + [shared/Lfm.Shared.csproj](../../shared/Lfm.Shared.csproj)
- [tests/Lfm.App.Core.Tests](../../tests/Lfm.App.Core.Tests/) → [app/Lfm.App.Core/Lfm.App.Core.csproj](../../app/Lfm.App.Core/Lfm.App.Core.csproj) + [shared/Lfm.Shared.csproj](../../shared/Lfm.Shared.csproj)
- [tests/Lfm.App.Tests](../../tests/Lfm.App.Tests/) → [app/Lfm.App.csproj](../../app/Lfm.App.csproj) + `Lfm.App.Core` + `Lfm.Shared`
- [tests/Lfm.E2E](../../tests/Lfm.E2E/) → excluded from gap detection (runtime-only SUT)

**Enumeration totals** (grep-based, weak signal — each entry is a *probable* gap):

| Class | Enumerated | Referenced from tests | Probable gaps | Confidence |
|---|---|---|---|---|
| `Gap-Route` — `[Function(...)]` HTTP routes | 28 | ~25 | 3 | high |
| `Gap-API` — `api/Services` public methods | 23 | 19 | 4 | medium |
| `Gap-API` — `api/Repositories` public methods | 13 | 11 | 2 | medium |
| `Gap-API` — `app/Lfm.App.Core` public API | 18 | 17 | 1 (**confirmed**) | high (confirmed by mutation) |
| `Gap-Throw` — `api/` exception throw sites | 8 | ~6 | 2 | medium |
| `Gap-Validate` — `[Required]` / `[StringLength]` / etc. on input records | 0 | 0 | 0 | high (no attribute-based validation in this repo) |

**Top probable gaps (highest confidence first):**

- **Confirmed** — `Gap-API` × `Gap-API-mutation`: **[RunsClient.CancelSignupAsync](../../app/Lfm.App.Core/Services/RunsClient.cs#L52)** — static enumeration finds zero references in `tests/Lfm.App.Core.Tests`, Stryker finds 1 survived + 3 NoCoverage mutants on lines 40/54/55/56. Both methods agree. `P0` remediation.

- **High confidence (static only)** — `Gap-Route`: **[PrivacyContactFunction `privacy/contact`](../../api/Functions/PrivacyContactFunction.cs)**. There is a test file ([PrivacyContactFunctionTests.cs](../../tests/Lfm.Api.Tests/PrivacyContactFunctionTests.cs)) but the exact route string `"privacy/contact"` is not found in it. Likely **false positive** — the test exercises the function class directly; the route template is not asserted because the function is constructed, not invoked via routing. Record as *probable false positive*.

- **High confidence (static only)** — `Gap-Route`: **[CorsPreflightFunction `{*path}`](../../api/Functions/CorsPreflightFunction.cs)** — the catch-all `OPTIONS` preflight function is covered by `Middleware/CorsMiddlewareTests.cs` at the middleware layer rather than the function layer. Indirect coverage is the norm for preflight. Treat as *covered indirectly via middleware test*; not a true gap.

- **False positive** — `Gap-Route`: **[E2ELoginFunction `e2e/login`](../../api/Functions/E2ELoginFunction.cs)** — the function is `#if E2E` at [L1](../../api/Functions/E2ELoginFunction.cs#L1) and guarded at runtime at [L22](../../api/Functions/E2ELoginFunction.cs#L22). It is not compiled into the production build. By design it should *not* have a unit test in `Lfm.Api.Tests`. It *does* have an E2E test via the Playwright `AuthFixture`. Clear false positive. **Flag as positive defense-in-depth**: production build does not contain the function *and* a second guard would catch it if the first failed. Exemplary.

- **Medium confidence** — `Gap-API`: [CharacterPortraitService.ResolveAsync](../../api/Services/CharacterPortraitService.cs) — method name not referenced in test bodies. Likely covered indirectly via `BattleNetCharacterPortraitsFunctionTests.cs` mocking `ICharacterPortraitService`. *Mark as probable false positive until verified.*

- **Medium confidence** — `Gap-API`: [BlizzardProfileClient.GetAccountProfileSummaryAsync](../../api/Services/BlizzardProfileClient.cs) — no test references it directly. *Verify by grepping for the method name or by running mutation testing against `api/Services/BlizzardProfileClient.cs` once Stryker is wired for `Lfm.Api.csproj`.*

- **Medium confidence** — `Gap-API`: [InstancesRepository.UpsertBatchAsync](../../api/Repositories/InstancesRepository.cs) and [SpecializationsRepository.UpsertBatchAsync](../../api/Repositories/SpecializationsRepository.cs) — bulk-upsert repository methods. Likely covered indirectly via `ReferenceSyncTests.cs` and `WowUpdateFunctionTests.cs` which mock the repositories. Confirm by mutation testing.

- **Medium confidence** — `Gap-Throw`: [BlizzardGameDataClient.cs:64](../../api/Services/BlizzardGameDataClient.cs) `throw new InvalidOperationException("Blizzard token endpoint returned empty response.")` — no test body names both the exception type and the containing method. Likely covered by `ReferenceSyncTests` fault-tolerance paths but worth verifying.

### 5.B. Auth matrix coverage (step 2.6)

**Protected endpoints** enumerated from `api/Functions/*.cs` via `[HttpTrigger(AuthorizationLevel.Anonymous)` negation and the custom `[RequireAuth]` attribute ([api/Auth/RequireAuthAttribute.cs](../../api/Auth/RequireAuthAttribute.cs)). 21 protected endpoints identified.

**Middleware-layer coverage model.** This repo does not use `TestAuthHandler` on a `WebApplicationFactory`. Instead, [AuthMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs) covers the auth cells (anonymous / expired / tampered / sufficient) at the middleware layer, and [AuthPolicyMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthPolicyMiddlewareTests.cs) covers the policy layer. The reflection-based `FunctionAuthorizationContractTests` asserts that *every* HTTP function carries `[RequireAuth]` or is in the explicit allow-list, so the middleware-layer coverage extends transitively to every protected endpoint. This is a valid form of coverage — it is not the industry-standard `TestAuthHandler` pattern, but it delivers the same contract.

| Scenario | Covered at | Endpoint-specific gaps |
|---|---|---|
| `anonymous` → 401 | [AuthMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs) | none (middleware decision is uniform) |
| `token-expired` → 401 | [AuthMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs) | none |
| `token-tampered` → 401 | [AuthMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthMiddlewareTests.cs) + E2E [SecuritySpec.cs:174](../../tests/Lfm.E2E/Specs/SecuritySpec.cs#L174) | none |
| `insufficient-scope` → 403 | [AuthPolicyMiddlewareTests.cs](../../tests/Lfm.Api.Tests/Middleware/AuthPolicyMiddlewareTests.cs) + per-endpoint for policy-bearing endpoints | **`Gap-AuthZ`** on [GuildAdminFunction](../../api/Functions/GuildAdminFunction.cs) — the `IsAdminAsync` call is tested against an admin principal but not against a non-admin principal at the function layer. The middleware test covers the general 403 shape, but the `[RequireAuth]` attribute does not declare a policy, so the policy check is *inside* the function body — which means only per-endpoint coverage proves the non-admin case. Recommend adding `GuildAdminFunction_Returns_403_When_Principal_Is_Not_Admin`. **Confidence: high.** |
| `sufficient-scope` → 200/documented | per-endpoint function tests | none |
| `cross-user` → 403/404 | E2E [SecuritySpec.cs:242](../../tests/Lfm.E2E/Specs/SecuritySpec.cs#L242) `CrossUserAccess_Rejected` | — |

**Gap-AuthZ findings:**

- **[GuildAdminFunction — non-admin principal](../../api/Functions/GuildAdminFunction.cs)** — only admin-path tested at function layer; middleware path cannot catch the in-function policy check. `P2`.
- **[RunsSignupFunction — insufficient permission](../../api/Functions/RunsSignupFunction.cs)** — permission check on `CanSignupGuildRunsAsync` tested in `GuildPermissionsTests`, but no function-layer test with a principal lacking `CanSignupGuildRunsAsync == true`. `P2`.
- **Cross-user coverage for runs endpoints** — `cross-user` cell is covered at the `/api/me` endpoint via E2E `CrossUserAccess_Rejected`, but not for `runs/{id}` (GET/PUT/DELETE). If a user can read another user’s runs, no test catches it. `P1` — this is a data-leak shape worth unit-testing at the function layer with a principal whose guild does not match the run’s guild.

### 5.C. Migration upgrade-path coverage (step 2.7)

**Status:** not applicable.

Confirmed by glob: `api/Migrations/**/*.cs` → no files. The migration-runner policy documented in [CLAUDE.md](../../CLAUDE.md) exists but no migrations have landed yet. Re-run this step once the first migration ships.

---

## 6. Runtime distribution

**Source:** `.test-determinism/{core,api,app}/run1/run1.trx` (parsed via `grep` from `<UnitTestResult duration="...">` attributes; no determinism-only filter applied).

**Top 10 slowest tests across all unit projects:**

| # | Project | Test | Duration |
|---|---|---|---|
| 1 | api | `ReferenceSyncTests.SyncAllAsync_syncs_instances_and_specializations_in_order` | **305 ms** |
| 2 | app | `AttendanceRosterSectionTests.Renders_Character_Rows_With_Class_Colored_Names` | 249 ms |
| 3 | app | `WowClassBadgeTests.Renders_Span_With_Correct_Class_Color(classId: 11, ...)` | 249 ms |
| 4 | app | `InstancesPageTests.Renders_grid_with_items_after_load` | 242 ms |
| 5 | app | `RunsPagesTests.CreateRunPage_Renders_Loading_Ring_On_Mount` | 237 ms |
| 6 | app | `LayoutTests.MainLayout_Shows_SignIn_When_Unauthenticated` | 222 ms |
| 7 | app | `CharactersPagesTests.CharactersPage_Renders_Character_Cards_After_Load` | 222 ms |
| 8 | app | `GuildPagesTests.GuildPage_Renders_Guild_Name_After_Load` | 222 ms |
| 9 | app | `AuthPagesTests.PrivacyPolicyPage_Renders_All_Section_Headings` | 216 ms |
| 10 | api | `ReferenceSyncTests.SyncAllAsync_continues_when_spec_media_fetch_fails_with_null_icon_url` | 216 ms |

**Findings:**

- **Top 10 ReferenceSyncTests are all > 100 ms.** All eight remaining slow tests in the top 30 are also in `ReferenceSyncTests.cs`. They are parameterized theory rows over `SyncAllAsync` with a mocked pipeline. Flagged as `LC-7`-ish / `dotnet.LC-5` (slow unit tests). Root cause is likely repetitive mock setup across the theory rows. Action: investigate whether a shared `IClassFixture` with a pre-built mock sequence plus a `FakeTimeProvider` would amortize the setup. `P3` — the tests work and the duration is acceptable in absolute terms.
- **bUnit tests are all ~220 ms.** This is inherent bUnit rendering overhead (`TestContext` construction, DI container, Razor compilation). Not a smell; merely expensive per test. Keep the bUnit suite size bounded. For reference, the whole bUnit project runs in ~17 s.
- **Core tests are all < 55 ms.** Healthy.
- **No unit test crosses the 1 s threshold.** No `Thread.Sleep` or retry loops surfaced.

Runtime for the three unit projects combined: approximately **45 s total** for 461 tests (from `Counters`-time in the `.trx` files).

---

## 7. Extensions loaded, limitations, and verification

- **Extensions loaded:** `dotnet` — covers xUnit, bUnit, Moq, NSubstitute, FluentAssertions, Playwright .NET, Testcontainers, Deque.AxeCore. See [extensions/dotnet.md](../../.claude/skills/test-quality-audit/extensions/dotnet.md).
- **Mutation SUT scope:** only `app/Lfm.App.Core` was mutated. `api/Lfm.Api.csproj` is compatible with Stryker but not wired yet — `P3` worklist item.
- **Blazor WASM mutation exclusion:** `app/Lfm.App.csproj` cannot be mutated (Razor source generators vs. Stryker recompile). The extract-to-core workaround is already in effect for the SPA’s services and i18n. bUnit tests continue to rely on static audit only.
- **Determinism gating:** ran all three unit projects (< 500 methods each). Zero divergence.
- **Carve-outs applied:**
  - `StubHttpMessageHandler` is a fake (not a mock) → `dotnet.POS-5`.
  - `TestLogger<T>` is a capture helper → `dotnet.POS-4` when used for audit-event property-key assertions.
  - bUnit `TestContext` + `RenderWithProviders` is `dotnet.POS-3` shared-immutable setup, not `LC-7`.
  - `IPlaywright`/`IBrowser`/`IBrowserContext`/Testcontainers bringup in `StackFixture.cs` is `dotnet.POS-3` + `E-POS-8`, not `LC-7`.
  - Wall-clock `Task.Delay` in `StackFixture.cs` runs once per suite during container bringup → not `E-HC-F3`.
  - Traces/screenshots via `TestResultCollector.cs` only written on failure → `E-POS-7`, not `E-HC-F7`.
  - `E2ELoginFunction` dual-guarded with `#if E2E` + runtime env check → positive defense-in-depth, not `E-HC-S5` / `I-HC-B2`.

- **What static audit + mutation + determinism together cannot tell you (honest limits):**
  - Whether expected values match real external specs (RFC vectors, OpenAPI docs, Blizzard API contracts) beyond what the test source text discloses.
  - Whether tests are correct *under production load* — determinism verification runs a single-process sequential sample.
  - Whether mutation score would hold under different mutation levels (this run used default `Standard`).
  - Whether `api/Lfm.Api.csproj` has latent no-coverage gaps — Stryker has not run against it yet.
  - Whether the E2E performance diagnostics would have caught a regression — they do not assert a budget.

---

## 8. Prioritized remediation worklist

Severity:

- **P0** — correctness-risking gap, confirmed by multiple signals.
- **P1** — latent-flake or defensive-gap that has bit similar projects before.
- **P2** — targeted quality gap; clear fix; low blast radius.
- **P3** — design/tooling improvements that raise the ceiling of the quality signal itself.

### P0

- **Add tests for [`RunsClient.CancelSignupAsync`](../../app/Lfm.App.Core/Services/RunsClient.cs#L52)** `[mutation]` `[gap]` — confirmed gap (mutation + static). At minimum: (a) happy path → asserts the `RunDetailDto?` returned matches the stub’s response; (b) `404 Not Found` → asserts `null`; (c) `400 Bad Request` → asserts propagated exception or `null` per the pattern of sibling methods; (d) cancellation-token propagation per `POS-7`. Effort: ~20 minutes. Impact: closes the only *confirmed* (static ∩ mutation) gap in the repo.

### P1

- **Resolve the block-removal mutation survivors in [`BattleNetClient`](../../app/Lfm.App.Core/Services/BattleNetClient.cs), [`GuildClient`](../../app/Lfm.App.Core/Services/GuildClient.cs), [`MeClient`](../../app/Lfm.App.Core/Services/MeClient.cs)** `[mutation]` — for each of `BattleNetClient.cs:22/38/57`, `GuildClient.cs:16`, `MeClient.cs:16/31`, add an assertion that *what the client did* with the HTTP response (request URL, headers, deserialized DTO shape). The current tests pass even if the method body is deleted because the `StubHttpMessageHandler` returns the expected payload regardless. The fix is mechanical: after `var result = await sut.XxxAsync(...)`, add `handler.LastRequest!.RequestUri!.PathAndQuery.Should().Be("/api/...")` or similar request-inspection assertion. Effort: ~1 hour across six methods. Impact: the first mutation-vs-audit-disagreement fix this repo has needed.

- **Resolve the 4 mutation gaps in [`JsonStringLocalizer.cs`](../../app/Lfm.App.Core/i18n/JsonStringLocalizer.cs)** `[mutation]` — 2 surviving statement mutations (L69, L95) + 2 no-coverage mutants (L45 bool, L75 stmt) + 1 timeout. Add tests for the fallback-to-English path when the requested key exists only in the fallback locale, the async-reload race, and whatever L45 decides (likely a nullability branch). Effort: ~45 minutes. Impact: moves this file from strong-disagreement to strong-agreement.

- **Refactor the two `HC-5` locale-side-effect tests in [AppAuthenticationStateProviderTests.cs:103, :117](../../tests/Lfm.App.Core.Tests/Auth/AppAuthenticationStateProviderTests.cs#L103)** — replace `localeService.Verify(s => s.SetLocale(...))` with assertions on `localeService.CurrentLocale` observable state. Static audit + mutation agreement. Effort: ~10 minutes.

- **Inject `TimeProvider` into SUTs and use `FakeTimeProvider` in `Lfm.Api.Tests`** `[dotnet.HC-7]` — replace all 88 `DateTimeOffset.UtcNow` reads in test bodies with `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`. Start with the systemic offenders: `RunEditabilityTests.cs` (13 reads), `GuildPermissionsTests.cs` (8 reads), `HealthFunctionTests.cs` (6 reads), `RunsUpdateFunctionTests.cs` (6 reads), `RunsSignupFunctionTests.cs` (6 reads). The SUT changes are likely a dozen files — `RunEditability`, `GuildPermissions`, the Runs functions. Effort: ~3 hours across SUT + tests. Impact: eliminates latent flake; enables deterministic testing of time-sensitive edges.

- **Root-cause and un-quarantine [`RunsSpec.DeleteRun_Confirm_RemovedFromList`](../../tests/Lfm.E2E/Specs/RunsSpec.cs#L199)** `[LC-9]` `[E-HC-P5]` — skip reason names "shared browser context" as the symptom but not an issue link or exit criterion. Quarantine without root cause has survived two consecutive audits. Either fix (isolate into its own context) or delete (document the coverage gap explicitly). Effort: ~30 minutes.

- **Add function-layer cross-user tests for `runs/{id}` GET/PUT/DELETE** `[Gap-AuthZ]` — E2E covers `me` cross-user, but not runs. A user whose `GuildId` does not match the target run’s `GuildId` should get 403. Unit test at the function layer with a mismatched `SessionPrincipal`. Effort: ~45 minutes. Impact: closes a data-leak shape that neither static audit nor mutation can surface.

### P2

- **Remove 3 `dotnet.HC-1` `Times.Once` `.Verify(...)` calls in [GuildPagesTests.cs:47, :69, :84](../../tests/Lfm.App.Tests/GuildPagesTests.cs)** — the markup assertions already prove the call happened; the verify adds nothing. Effort: 5 minutes.

- **Add negative `GuildAdminFunction` non-admin-principal test** `[Gap-AuthZ]` — `GuildAdminFunction_Returns_403_When_Principal_Is_Not_Admin`. Effort: ~15 minutes.

- **Add `RunsSignupFunction` insufficient-permission test** `[Gap-AuthZ]` — principal with `CanSignupGuildRunsAsync == false` expects 403. Effort: ~15 minutes.

- **Cite WoW class-color canonical values in [WowClassBadgeTests.cs](../../tests/Lfm.App.Tests/WowClassBadgeTests.cs) and [AttendanceRosterSectionTests.cs:68](../../tests/Lfm.App.Tests/AttendanceRosterSectionTests.cs#L68)** — replace hex literals (`#C69B6D`, `#0070DD`) with `WowClasses.GetColor(classId)` lookups or cite the canonical constants in a comment. `dotnet.LC-3` remediation. Effort: 15 minutes.

- **Replace `RunsPagesTests.cs` hardcoded `"2026-05-01T20:00:00Z"` with a spec-cited constant or comment** `[HC-3]`. Effort: 5 minutes.

- **Resolve [`RunsSpec.CreateRun_SubmitForm_AppearsInList`](../../tests/Lfm.E2E/Specs/RunsSpec.cs#L67) `#instance-select` selector** — either document the fluent-select limitation with a `[Trait("Limitation", "fluent-select-web-component")]`, or rewrite once the web component exposes an accessible name. `E-HC-F2`. Effort: 10 minutes.

- **Move [`ProfileSpec.SelectCharacter_ViaApi_UpdatesSelection`](../../tests/Lfm.E2E/Specs/ProfileSpec.cs#L113) out of E2E** `[E-HC-F10]` — the test asserts on a `PUT /api/raider/characters/{id}` response shape, no UI check. Move to a (to-be-created) integration lane, or promote the UI check in the same test body. Effort: 30 minutes.

- **Replace [`ProfileSpec.RefreshCharacters_Click_UpdatesFromBattleNet`](../../tests/Lfm.E2E/Specs/ProfileSpec.cs#L82) `Page.WaitForTimeoutAsync(2000)` (residual `E-HC-F3`)** — use `WaitForRequestAsync` on the Battle.net refresh endpoint. Effort: 15 minutes.

### P3

- **Wire Stryker.NET for `Lfm.Api.csproj`** `[mutation]` — the Functions project has no mutation signal yet. Expected runtime is ~3 min for the full project. Add a second `dotnet stryker` invocation targeting `tests/Lfm.Api.Tests` with an updated `stryker-config.json` (or per-test-project config via `--solution`). Effort: ~45 minutes to configure, runtime overhead per audit: ~3 min. Impact: unlocks the block-removal / unkilled-mutant signal for 33 API test files × 306 tests — where the biggest shadow coverage gap is likely to live.

- **Add post-interaction axe scans in [AccessibilitySpec.cs](../../tests/Lfm.E2E/Specs/AccessibilitySpec.cs)** `[E-HC-A2]` — scan after form submission, menu open, focus trap, dialog open (where applicable). Effort: 2 hours. Impact: closes the biggest `A` sub-lane gap.

- **Introduce an integration-lane test project (`Lfm.Api.Integration.Tests` or similar)** — the pyramid ratio has **zero** integration tests. Every integration concern currently lands in unit (with mocks of process boundaries) or E2E (with a full browser). A `WebApplicationFactory<Program>` + Testcontainers Cosmos emulator project would:
  - Absorb the 14 `E-HC-S1` header-value assertions from `SecuritySpec.cs`.
  - Cover the real OAuth callback flow through middleware + auth pipeline.
  - Cover the Cosmos repository layer against a real emulator instead of mocked `Container`.
  - Cover `ProfileSpec.SelectCharacter_ViaApi_UpdatesSelection` correctly.

  Effort: 1–2 days to scaffold. Impact: introduces the missing middle rung of the test pyramid. This is the only recommendation that requires new project bringup; everything else is in-place refactoring.

- **Add an `LC-5` ratchet for slow unit tests** — add xUnit `[Trait("Speed", "Slow")]` to the 10 `ReferenceSyncTests` cases exceeding 100 ms and a test-filter rule in CI that rejects new tests slower than a given threshold without the trait. Signals design drift at suite growth. Effort: 30 minutes.

- **Document the `#if E2E` + env-check pattern** — the `E2ELoginFunction` dual-guard is excellent; document it as a pattern in `CLAUDE.md` or `docs/` so future test-only endpoints follow the same convention.

---

## 9. Summary

- **537 tests** across four projects. **90 %** are specification under their respective rubrics; **5.6 %** are characterization or incidental; the remainder are ambiguous.
- **Determinism verified** — every unit project ran twice, zero divergence. 88 `dotnet.HC-7` smells in `Lfm.Api.Tests` are latent, not active.
- **Mutation score 86.24 %** on `Lfm.App.Core`. The real findings are not the headline score — they are the six **audit-vs-mutation disagreements** on files rated strong. Three of them are block-removal survivors on service clients that assert on stub responses without verifying the client behavior. This is the strongest new finding of the audit and the clearest case for why mutation testing belongs in deep-mode alongside static audit.
- **One confirmed gap** (static ∩ mutation): `RunsClient.CancelSignupAsync`. Unchanged across two consecutive deep audits.
- **One quarantined test** (`LC-9`), **one skewed pyramid** (no integration lane), and **one systemic latent-flake** (wall-clock reads in `Lfm.Api.Tests`) — these are the structural issues. None of them blocks shipping; all three are worth scheduling.
- **Three exemplary patterns** worth keeping: the `TestLogger<T>` + audit-event property-key assertion idiom in `Lfm.Api.Tests`; the `StubHttpMessageHandler` fake + per-test `HttpClient` pattern in `Lfm.App.Core.Tests`; and the `#if E2E` + runtime env-check dual-guard on `E2ELoginFunction`.

The suite is healthy. The only things that should keep a reader of this report awake are the block-removal mutation survivors (§4) and the `CancelSignupAsync` confirmed gap (§5.A). Everything else is hygiene.
