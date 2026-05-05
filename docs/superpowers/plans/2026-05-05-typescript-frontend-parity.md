# TypeScript Frontend Parity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the frontend behavior that existed at git tag `the-last-typescript` and is missing from the current Blazor/.NET version, without recreating the old React/MUI architecture.

**Architecture:** Keep the current Blazor WASM, Fluent UI, app-core service, shared-contract, and Azure Functions boundaries. Restore behavior as focused vertical slices: first wire/API contracts where the current frontend cannot express the old interaction, then Razor components, then bUnit/API tests, then only E2E tests for browser-only behavior. Preserve the current .NET-era run create/edit model and route shape where it is stronger than the TypeScript version.

**Tech Stack:** .NET 10, Blazor WebAssembly, Microsoft Fluent UI Blazor, xUnit, bUnit, Azure Functions isolated worker, shared C# contracts, Playwright .NET for browser-only verification.

---

## Scope

This plan ports behavior found in the old TypeScript frontend at tag `the-last-typescript`. To inspect the old implementation during execution, use commands like:

```bash
git -C /home/souroldgeezer/repos/lfm show the-last-typescript:frontend/src/features/runs/components/RunSignupCard.tsx
git -C /home/souroldgeezer/repos/lfm show the-last-typescript:frontend/src/features/characters/pages/CharactersPage.tsx
git -C /home/souroldgeezer/repos/lfm show the-last-typescript:frontend/src/features/guild/pages/GuildPage.tsx
git -C /home/souroldgeezer/repos/lfm show the-last-typescript:frontend/src/features/guild/pages/GuildAdminPage.tsx
```

Do not port these as-is:

- Do not add a React, MUI, TanStack Query, or React Router compatibility layer.
- Do not re-add `/login/success` unless current API redirects still reference it. The current callback flow lands directly in the app.
- Do not replace current run create/edit support for activities, dungeon scopes, Mythic+ levels, ETags, or current validation limits with the older TypeScript form.
- Do not expose shared-contract properties unless `app/` or `app/Lfm.App.Core/` consumes them in the same task, per `docs/wire-payload-contract.md`.

## Target File Map

- `shared/Lfm.Contracts/Characters/CharacterDto.cs` - extend signup-option characters with available specialization choices.
- `shared/Lfm.Contracts/Runs/RunCharacterDto.cs` - expose current signup identity/spec fields needed by the Blazor signup editor.
- `shared/Lfm.Contracts/Runs/RunSignupOptionsDto.cs` - continue to wrap run-scoped signup options after character DTO enrichment.
- `api/Mappers/AccountCharacterMapper.cs` - map stored specialization summaries into character DTOs.
- `api/Mappers/RunResponseMapper.cs` - project run signup character id and spec id for frontend consumers.
- `api/Runs/RunSignupOptionsService.cs` - keep guild filtering and return enriched options.
- `api/Mappers/GuildMapper.cs` - restore correct `RequiresSetup` semantics for existing guilds.
- `api/Functions/GuildAdminFunction.cs` - add site-admin update endpoint for arbitrary guild settings.
- `app/Pages/RunsPage.razor` - delegate signup UI to a focused component and keep run grouping/detail ownership.
- `app/Components/Runs/RunSignupPanel.razor` - new signup parity component.
- `app/Components/Runs/SpecIcon.razor` - new small display component for specialization icons.
- `app/Pages/CharactersPage.razor` - add sort, pagination, and redirect-after-selection.
- `app/Components/ConfirmDialog.razor` - reusable confirmation dialog for cancel signup and future destructive prompts.
- `app/Services/UnsavedChangesGuard.cs` and `app/wwwroot/js/unsavedChanges.js` - Blazor navigation and beforeunload guard.
- `app/Pages/CreateRunPage.razor`, `app/Pages/EditRunPage.razor`, `app/Pages/GuildPage.razor` - wire dirty-state protection.
- `app/Pages/GuildPage.razor` - restore editor-facing setup/status/settings surface.
- `app/Pages/GuildAdminPage.razor` - restore site-admin cross-guild admin flow.
- `app/Layout/MainLayout.razor` - restore selected-character account menu while preserving theme, locale, source, and sign-out behavior.
- `app/wwwroot/locales/en.json` and `app/wwwroot/locales/fi.json` - add all restored UI strings.
- `tests/Lfm.Api.Tests/` - API/mapper/service tests for new contracts and admin endpoint.
- `tests/Lfm.App.Tests/` - bUnit tests for Blazor UI behavior.
- `tests/Lfm.E2E/` - targeted browser tests only for route blocking and full cross-page flows.

## Implementation Tasks

### Task 1: Restore Signup Wire Data

**Files:**
- Modify: `shared/Lfm.Contracts/Characters/CharacterDto.cs`
- Modify: `shared/Lfm.Contracts/Runs/RunCharacterDto.cs`
- Modify: `api/Mappers/AccountCharacterMapper.cs`
- Modify: `api/Mappers/RunResponseMapper.cs`
- Modify: `api/Runs/RunSignupOptionsService.cs`
- Test: `tests/Lfm.Api.Tests/Runs/RunSignupOptionsServiceTests.cs`
- Test: `tests/Lfm.Api.Tests/Mappers/RunResponseMapperTests.cs`

- [ ] **Step 1: Write failing API tests for signup options.**

  Add a test proving `RunSignupOptionsService.GetAsync` returns eligible characters with all stored specialization choices:

  ```csharp
  [Fact]
  public async Task GetAsync_Returns_Eligible_Characters_With_Specializations()
  {
      // Arrange a raider with one guild-roster character whose
      // SpecializationsSummary contains Arms and Fury, and a run in that guild.
      // Act through RunSignupOptionsService.GetAsync.
      // Assert RunSignupOptionsResult.Ok with one CharacterDto, ActiveSpecId 71,
      // SpecName "Arms", and Specializations containing ids 71 and 72.
  }
  ```

  Run:

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter GetAsync_Returns_Eligible_Characters_With_Specializations
  ```

  Expected: fail because `CharacterDto` does not expose specialization choices.

- [ ] **Step 2: Write failing mapper test for current signup identity.**

  Add a mapper test proving `RunResponseMapper.ToDetail` exposes `CharacterId` and `SpecId` for the current user's signup:

  ```csharp
  [Fact]
  public void ToDetail_Projects_CharacterId_And_SpecId_For_Run_Characters()
  {
      // Arrange RunDocument with RunCharacterEntry CharacterId "eu-silvermoon-arthas",
      // SpecId 71, and RaiderBattleNetId matching currentBattleNetId.
      // Act RunResponseMapper.ToDetail(run, "player#1234").
      // Assert dto.RunCharacters.Single().CharacterId == "eu-silvermoon-arthas"
      // and dto.RunCharacters.Single().SpecId == 71.
  }
  ```

  Run:

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter ToDetail_Projects_CharacterId_And_SpecId_For_Run_Characters
  ```

  Expected: fail because `RunCharacterDto` omits these fields.

- [ ] **Step 3: Add contract records and mapper projections.**

  Implement:

  ```csharp
  public sealed record CharacterSpecializationDto(int Id, string Name);

  public sealed record CharacterDto(
      string Name,
      string Realm,
      string RealmName,
      int Level,
      string Region,
      int? ClassId = null,
      string? ClassName = null,
      string? PortraitUrl = null,
      int? ActiveSpecId = null,
      string? SpecName = null,
      IReadOnlyList<CharacterSpecializationDto>? Specializations = null);
  ```

  Update `RunCharacterDto` so the new fields are live consumers, not reservations:

  ```csharp
  public sealed record RunCharacterDto(
      string CharacterId,
      string CharacterName,
      string CharacterRealm,
      int CharacterClassId,
      string CharacterClassName,
      string DesiredAttendance,
      string ReviewedAttendance,
      int? SpecId,
      string? SpecName,
      string? Role,
      bool IsCurrentUser);
  ```

  Update `AccountCharacterMapper` to fill `Specializations` from `StoredSpecializationsSummary.Specializations`. Update `RunResponseMapper.ToCharacter` to pass `character.CharacterId` and `character.SpecId`.

- [ ] **Step 4: Run focused API tests.**

  Run:

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter "GetAsync_Returns_Eligible_Characters_With_Specializations|ToDetail_Projects_CharacterId_And_SpecId_For_Run_Characters"
  ```

  Expected: pass.

- [ ] **Step 5: Commit.**

  ```bash
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add shared/Lfm.Contracts api tests/Lfm.Api.Tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Expose signup option metadata"
  ```

### Task 2: Restore Run Signup Interaction

**Files:**
- Create: `app/Components/Runs/RunSignupPanel.razor`
- Create: `app/Components/Runs/RunSignupPanel.razor.css`
- Create: `app/Components/Runs/SpecIcon.razor`
- Create: `app/Components/ConfirmDialog.razor`
- Modify: `app/Pages/RunsPage.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.App.Tests/RunsPagesTests.cs`

- [ ] **Step 1: Write failing bUnit tests for old signup behavior.**

  Add tests with these exact behaviors:

  ```csharp
  [Fact]
  public void RunsPage_CurrentSignup_Can_Change_Attendance_Without_Canceling()
  {
      // Arrange selected run with IsCurrentUser signup CharacterId "char-1",
      // DesiredAttendance "IN", and future signup close.
      // Render RunsPage, click BENCH in the signup toggle.
      // Verify IRunsClient.SignupAsync("run-1", SignupRequest("char-1", "BENCH", 71), ...)
      // is called once.
  }

  [Fact]
  public void RunsPage_CurrentSignup_Can_Open_ChangeCharacter_Panel()
  {
      // Arrange current signup plus signup options for two characters.
      // Render RunsPage, click the localized change-character button.
      // Assert character select and spec select are visible.
  }

  [Fact]
  public void RunsPage_CancelSignup_Requires_Confirmation()
  {
      // Arrange current signup.
      // Click cancel once and verify IRunsClient.CancelSignupAsync is not called.
      // Click confirm in ConfirmDialog and verify CancelSignupAsync is called once.
  }

  [Fact]
  public void RunsPage_Signup_Shows_GuildRankBlocked_Message_When_Options_Are_Forbidden()
  {
      // Arrange GetSignupOptionsAsync returning Error or forbidden-shaped client result
      // after the service reports guild-rank-denied.
      // Assert the localized guild rank blocked message is shown.
  }
  ```

  Run:

  ```bash
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "RunsPage_CurrentSignup_Can_Change_Attendance_Without_Canceling|RunsPage_CurrentSignup_Can_Open_ChangeCharacter_Panel|RunsPage_CancelSignup_Requires_Confirmation|RunsPage_Signup_Shows_GuildRankBlocked_Message_When_Options_Are_Forbidden"
  ```

  Expected: fail because current `RunsPage` only allows initial signup plus direct cancel.

- [ ] **Step 2: Extract signup panel.**

  Move the signup markup and state from `RunsPage.razor` into `RunSignupPanel.razor`. Keep the public component contract narrow:

  ```razor
  <RunSignupPanel Run="@selectedRun"
                  CurrentSignup="@currentSignup"
                  SignupCharacters="@_signupCharacters"
                  OptionsLoading="@_signupOptionsLoading"
                  Submitting="@_signupSubmitting"
                  CancelSubmitting="@_cancelSubmitting"
                  Error="@_signupError"
                  OnEnsureOptions="@EnsureSignupOptionsLoaded"
                  OnSubmit="@SubmitSignup"
                  OnCancel="@CancelSignup" />
  ```

  Keep API calls in `RunsPage.razor`; make `RunSignupPanel` a UI/state component that emits `SignupRequest` and cancel intents.

- [ ] **Step 3: Add existing-signup update behavior.**

  In the panel, when the current user already has a signup and the change-character editor is closed, clicking an attendance toggle submits:

  ```csharp
  new SignupRequest(
      CharacterId: CurrentSignup.CharacterId,
      DesiredAttendance: selectedAttendance,
      SpecId: CurrentSignup.SpecId)
  ```

  Keep no-op behavior when the user clicks the already-selected attendance and no character/spec change is active.

- [ ] **Step 4: Add change-character and spec selection.**

  Show a compact current-signup row with character name, spec icon when available, and a change-character button. When opened, show character select, spec select populated from `CharacterDto.Specializations`, and a back button. Default to the current signup character if available; otherwise default to `/api/v1/me` selected character; otherwise first eligible option.

- [ ] **Step 5: Add cancel confirmation.**

  Implement `ConfirmDialog.razor` with a Fluent dialog or native dialog wrapper that supports:

  ```razor
  <ConfirmDialog Open="@_pendingCancel"
                 Title="@Loc["runs.signup.cancelConfirmTitle"]"
                 Body="@Loc["runs.signup.cancelConfirmBody"]"
                 ConfirmLabel="@Loc["runs.signup.cancelConfirmConfirm"]"
                 CancelLabel="@Loc["runs.signup.cancelConfirmCancel"]"
                 ConfirmIntent="MessageIntent.Error"
                 OnConfirm="@ConfirmCancel"
                 OnCancel="@CloseCancelDialog" />
  ```

  Verify focus returns to the cancel button after closing in a bUnit-accessible way by asserting the button remains rendered and enabled; browser-level focus can be covered only if an E2E route is added.

- [ ] **Step 6: Add localization keys.**

  Add English and Finnish strings for:

  - `runs.signup.changeCharacter`
  - `runs.signup.back`
  - `runs.signup.spec`
  - `runs.signup.unknownSpec`
  - `runs.signup.cancelConfirmTitle`
  - `runs.signup.cancelConfirmBody`
  - `runs.signup.cancelConfirmConfirm`
  - `runs.signup.cancelConfirmCancel`
  - `runs.signup.guildRankBlocked`

- [ ] **Step 7: Run focused app tests.**

  ```bash
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "RunsPage_CurrentSignup_Can_Change_Attendance_Without_Canceling|RunsPage_CurrentSignup_Can_Open_ChangeCharacter_Panel|RunsPage_CancelSignup_Requires_Confirmation|RunsPage_Signup_Shows_GuildRankBlocked_Message_When_Options_Are_Forbidden"
  ```

  Expected: pass.

- [ ] **Step 8: Commit.**

  ```bash
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add app tests/Lfm.App.Tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Restore run signup controls"
  ```

### Task 3: Restore Character Sort, Pagination, and Redirect

**Files:**
- Modify: `app/Pages/CharactersPage.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.App.Tests/CharactersPagesTests.cs`

- [ ] **Step 1: Write failing bUnit tests.**

  Add:

  ```csharp
  [Fact]
  public void CharactersPage_Sorts_By_Name_When_Query_Sort_Name()
  {
      // Arrange characters "Zed" and "Aelrin".
      // Set navigation to /characters?sort=name.
      // Assert "Aelrin" appears before "Zed" in markup.
  }

  [Fact]
  public void CharactersPage_Paginates_Characters_And_Updates_Query()
  {
      // Arrange at least four characters and set page size to the mobile/default test page size.
      // Render /characters.
      // Click Next.
      // Assert the NavigationManager URI contains page=2 and the second page card is rendered.
  }

  [Fact]
  public void CharactersPage_Navigates_To_Safe_Redirect_After_Select()
  {
      // Render /characters?redirect=/runs/new with one enriched character.
      // Click the character.
      // Verify IMeClient.SelectCharacterAsync was called and NavigationManager.Uri ends with /runs/new.
  }
  ```

  Run:

  ```bash
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "CharactersPage_Sorts_By_Name_When_Query_Sort_Name|CharactersPage_Paginates_Characters_And_Updates_Query|CharactersPage_Navigates_To_Safe_Redirect_After_Select"
  ```

  Expected: fail because current `/characters` has no sort, page, or redirect behavior.

- [ ] **Step 2: Add query parameters and state helpers.**

  In `CharactersPage.razor`, add `[SupplyParameterFromQuery]` properties for `sort`, `page`, and `redirect`. Add helpers:

  ```csharp
  private const int DesktopPageSize = 9;
  private const int MobilePageSize = 3;
  private string CurrentSort => string.Equals(Sort, "name", StringComparison.Ordinal) ? "name" : "level";
  private int CurrentPage => int.TryParse(Page, out var p) && p > 0 ? p : 1;
  private string RedirectPath => !string.IsNullOrWhiteSpace(Redirect) && Redirect.StartsWith("/", StringComparison.Ordinal) ? Redirect : "/runs";
  ```

  Use a stable page size for bUnit. If responsive page size needs browser viewport detection later, keep it out of this task and add E2E coverage before changing it.

- [ ] **Step 3: Add sort and pagination UI.**

  Use Fluent select/buttons. Keep card grid dimensions stable. Preserve current enrich queue and portrait loading for only visible cards.

- [ ] **Step 4: Navigate after successful selection.**

  After `MeClient.SelectCharacterAsync` returns true, call:

  ```csharp
  Nav.NavigateTo(RedirectPath, forceLoad: false);
  ```

  Reject external redirect values by falling back to `/runs`.

- [ ] **Step 5: Run focused tests and commit.**

  ```bash
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "CharactersPage_Sorts_By_Name_When_Query_Sort_Name|CharactersPage_Paginates_Characters_And_Updates_Query|CharactersPage_Navigates_To_Safe_Redirect_After_Select"
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add app tests/Lfm.App.Tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Restore character list state"
  ```

### Task 4: Add Unsaved Changes Guard

**Files:**
- Create: `app/Services/UnsavedChangesGuard.cs`
- Create: `app/wwwroot/js/unsavedChanges.js`
- Modify: `app/Program.cs`
- Modify: `app/Pages/CreateRunPage.razor`
- Modify: `app/Pages/EditRunPage.razor`
- Modify: `app/Pages/GuildPage.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.App.Tests/UnsavedChangesGuardTests.cs`
- Test: `tests/Lfm.App.Tests/RunsPagesTests.cs`
- Test: `tests/Lfm.App.Tests/GuildPagesTests.cs`

- [ ] **Step 1: Write failing service tests.**

  Add tests proving the guard blocks internal navigation only when dirty:

  ```csharp
  [Fact]
  public async Task LocationChangingHandler_PreventsNavigation_WhenDirty()
  {
      // Register guard with dirty callback returning true.
      // Simulate LocationChangingContext for /runs.
      // Assert PreventNavigation was requested and dialog state is visible.
  }

  [Fact]
  public async Task LocationChangingHandler_AllowsNavigation_WhenClean()
  {
      // Register guard with dirty callback returning false.
      // Assert navigation is not prevented.
  }
  ```

- [ ] **Step 2: Implement guard and JS module.**

  `UnsavedChangesGuard` responsibilities:

  - Register/unregister `NavigationManager.RegisterLocationChangingHandler`.
  - Track one active dirty-state callback per owning component.
  - Import `./js/unsavedChanges.js`.
  - Enable `beforeunload` when dirty, disable it when clean/disposed.
  - Expose state that pages render with a `ConfirmDialog`.

  JS module:

  ```javascript
  let enabled = false;
  function handler(event) {
    if (!enabled) return;
    event.preventDefault();
    event.returnValue = "";
  }
  export function setEnabled(next) {
    enabled = Boolean(next);
    globalThis.removeEventListener("beforeunload", handler);
    if (enabled) globalThis.addEventListener("beforeunload", handler);
  }
  ```

- [ ] **Step 3: Wire forms.**

  For create/edit run pages, dirty means form state differs from initial state after load. For guild settings, dirty means draft settings differ from last loaded/saved DTO. After successful save, reset the baseline before navigation.

- [ ] **Step 4: Add localization keys.**

  Add:

  - `unsavedChanges.title`
  - `unsavedChanges.body`
  - `unsavedChanges.stay`
  - `unsavedChanges.leave`

- [ ] **Step 5: Run focused app tests and commit.**

  ```bash
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "UnsavedChanges|CreateRunPage|EditRunPage|GuildPage"
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add app tests/Lfm.App.Tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Add unsaved changes guard"
  ```

### Task 5: Restore Guild Setup Gate and Guild Home Editor

**Files:**
- Modify: `api/Mappers/GuildMapper.cs`
- Modify: `app/App.razor` or create `app/Components/Guild/GuildSetupGate.razor`
- Modify: `app/Pages/GuildPage.razor`
- Modify: `app/Components/Guild/GuildSettingsEditor.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.Api.Tests/GuildMapperTests.cs`
- Test: `tests/Lfm.App.Tests/GuildPagesTests.cs`
- Test: `tests/Lfm.App.Tests/RunsPagesTests.cs`

- [ ] **Step 1: Write failing mapper test for setup.**

  ```csharp
  [Fact]
  public void MapToDto_Marks_Existing_Admin_Guild_As_RequiringSetup_When_NotInitialized()
  {
      // Arrange GuildDocument with Setup null and GuildEffectivePermissions.IsAdmin true.
      // Assert dto.Setup.RequiresSetup is true and dto.Setup.IsInitialized is false.
  }
  ```

- [ ] **Step 2: Fix `GuildMapper`.**

  Set:

  ```csharp
  var isInitialized = doc.Setup?.InitializedAt is not null;
  var setup = new GuildSetupDto(
      IsInitialized: isInitialized,
      RequiresSetup: permissions.IsAdmin && !isInitialized,
      RankDataFresh: IsRosterFresh(doc),
      Timezone: doc.Setup?.Timezone ?? "Europe/Helsinki",
      Locale: doc.Setup?.Locale ?? "fi");
  ```

- [ ] **Step 3: Add route gate tests.**

  Add bUnit tests proving an editor with `RequiresSetup = true` is redirected from `/runs`, `/runs/new`, and `/runs/run-1/edit` to `/guild?setup=required`.

- [ ] **Step 4: Add Blazor setup gate.**

  Prefer a focused `GuildSetupGate.razor` used around protected run pages, or a small page-level check if the router structure makes a shared wrapper intrusive. The gate must show a loading state while `IGuildClient.GetAsync` runs and must not redirect non-editors.

- [ ] **Step 5: Restore guild home editor surface.**

  In `GuildPage.razor`, render:

  - setup-required explanation when query is `setup=required`
  - settings live/pending chip
  - editable/read-only chip
  - rank sync fresh/not configured chip
  - member count and rank count chips
  - `GuildSettingsEditor` for `dto.Editor.CanEdit`
  - rank-sync stale warning when `!dto.Setup.RankDataFresh`

  Do not add `SyncedMemberCount`, `AchievementPoints`, `MatchedRank`, or admin override metadata in this task unless a visible control consumes them.

- [ ] **Step 6: Run focused tests and commit.**

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter MapToDto_Marks_Existing_Admin_Guild_As_RequiringSetup_When_NotInitialized
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter "GuildPage|GuildSetupGate|RunsPage"
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add api app tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Restore guild setup flow"
  ```

### Task 6: Restore Site-Admin Cross-Guild Admin

**Files:**
- Modify: `shared/Lfm.Contracts/Guild/UpdateGuildRequest.cs`
- Modify: `api/Functions/GuildAdminFunction.cs`
- Modify: `api/openapi.json` or the source used to generate it if present
- Modify: `app/Lfm.App.Core/Services/GuildClient.cs`
- Modify: `app/Lfm.App.Core/Services/IGuildClient.cs`
- Modify: `app/Pages/GuildAdminPage.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.Api.Tests/GuildAdminFunctionTests.cs`
- Test: `tests/Lfm.App.Core.Tests/Services/GuildClientTests.cs`
- Test: `tests/Lfm.App.Tests/GuildPagesTests.cs`

- [ ] **Step 1: Write failing API tests for admin update.**

  Add:

  ```csharp
  [Fact]
  public async Task PatchV1_Updates_ArbitraryGuild_WhenCallerIsSiteAdmin()
  {
      // Arrange site admin, guildId=99, existing GuildDocument, and UpdateGuildRequest.
      // Act GuildAdminFunction.PatchV1.
      // Assert IGuildRepository.ReplaceAsync or Upsert path persisted the changed settings.
  }

  [Fact]
  public async Task PatchV1_Returns403_WhenCallerIsNotSiteAdmin()
  {
      // Arrange non-admin.
      // Assert 403 admin-only and no repository write.
  }
  ```

- [ ] **Step 2: Add admin client methods.**

  Extend `IGuildClient` with:

  ```csharp
  Task<GuildDto?> GetAdminAsync(string guildId, CancellationToken ct);
  Task<GuildDto?> UpdateAdminAsync(string guildId, UpdateGuildRequest request, CancellationToken ct);
  ```

  Implement paths:

  - GET `api/v1/guild/admin?guildId={escaped}`
  - PATCH `api/v1/guild/admin?guildId={escaped}`

  Keep current `GetAsync` and `UpdateAsync` for current-guild editing.

- [ ] **Step 3: Add backend PATCH.**

  Add `patch` triggers beside existing admin GET. Reuse the same validation and setting mutation logic as `GuildFunction` so current-guild and site-admin updates cannot drift. Emit an audit event with:

  - action `guild.admin.update`
  - actor battleNetId
  - target guild id
  - outcome success/failure

- [ ] **Step 4: Rebuild `GuildAdminPage`.**

  Page behavior:

  - Require `AuthorizeView Roles="SiteAdmin"` in UI.
  - Show guild id input and load button.
  - Call `GuildClient.GetAdminAsync`.
  - Render guild identity, guild id chip, rank freshness, member count, rank count.
  - Render `GuildSettingsEditor` using `UpdateAdminAsync`.
  - Show save success/failure message.

  Keep current-guild editing on `/guild` after Task 5; do not use `/guild/admin` for regular guild admins.

- [ ] **Step 5: Run focused tests and commit.**

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter GuildAdminFunctionTests
  dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release --filter GuildClientTests
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter GuildAdminPage
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add api app shared tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Restore site admin guild editor"
  ```

### Task 7: Restore Selected-Character Account Menu

**Files:**
- Modify: `app/Layout/MainLayout.razor`
- Modify: `app/Lfm.App.Core/Services/MeClient.cs`
- Modify: `shared/Lfm.Contracts/Me/MeResponse.cs`
- Modify: `api/Functions/MeFunction.cs`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.Api.Tests/MeFunctionTests.cs`
- Test: `tests/Lfm.App.Core.Tests/Auth/AppAuthenticationStateProviderTests.cs`
- Test: `tests/Lfm.App.Tests/LayoutTests.cs` or the existing layout test file

- [ ] **Step 1: Write failing tests.**

  Add tests proving:

  - `/api/v1/me` includes selected character display name and portrait URL when known.
  - `MainLayout` renders an account menu button with that name.
  - `MainLayout` keeps the existing theme, locale, source, and sign-out controls.
  - Site admins see the admin item.

- [ ] **Step 2: Extend `MeResponse` narrowly.**

  Add a nested nullable summary:

  ```csharp
  public sealed record SelectedCharacterSummaryDto(
      string Id,
      string Name,
      string? PortraitUrl);
  ```

  Add `SelectedCharacter` to `MeResponse`. This is a live layout consumer and avoids loading the full characters list from the layout.

- [ ] **Step 3: Render Fluent account menu.**

  In `MainLayout.razor`, replace plain sign-out button with an account trigger when authorized. Include:

  - selected character avatar or initial
  - selected character name with truncation
  - menu item `/characters`
  - menu item `/guild`
  - site-admin item `/admin/reference`
  - sign-out action using the existing top-level logout navigation

  Preserve the mobile nav toggle and close menus on navigation.

- [ ] **Step 4: Run focused tests and commit.**

  ```bash
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release --filter MeFunction
  dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release --filter AppAuthenticationStateProviderTests
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter MainLayout
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add api app shared tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Restore account menu"
  ```

### Task 8: Add Runs Continuation Loading Without Reverting Horizon Grouping

**Files:**
- Modify: `shared/Lfm.Contracts/Runs/RunsListResponse.cs`
- Modify: `app/Lfm.App.Core/Services/IRunsClient.cs`
- Modify: `app/Lfm.App.Core/Services/RunsClient.cs`
- Modify: `app/Pages/RunsPage.razor`
- Modify: `app/wwwroot/locales/en.json`
- Modify: `app/wwwroot/locales/fi.json`
- Test: `tests/Lfm.App.Core.Tests/Services/RunsClientTests.cs`
- Test: `tests/Lfm.App.Tests/RunsPagesTests.cs`

- [ ] **Step 1: Write failing client/page tests.**

  Add:

  ```csharp
  [Fact]
  public async Task ListPageAsync_Preserves_ContinuationToken()
  {
      // Arrange response with Items one run and ContinuationToken "next-token".
      // Assert RunsClient exposes both items and token.
  }

  [Fact]
  public void RunsPage_LoadMore_Appends_Runs()
  {
      // Arrange first page with continuation token and second page with another run.
      // Click load more.
      // Assert both runs render and ListPageAsync was called with the continuation token.
  }
  ```

- [ ] **Step 2: Add paged client API.**

  Add:

  ```csharp
  Task<RunsListResponse> ListPageAsync(string? continuationToken, CancellationToken ct);
  ```

  Keep existing `ListAsync` as a convenience wrapper for current callers.

- [ ] **Step 3: Add Load More UI.**

  Keep current horizon grouping and past toggle. Add a load-more button after the sidebar/list when `ContinuationToken` is not null. Do not re-add old manual sort unless the user asks for it; the current horizon ordering is more task-focused.

- [ ] **Step 4: Run focused tests and commit.**

  ```bash
  dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release --filter ListPageAsync_Preserves_ContinuationToken
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release --filter RunsPage_LoadMore_Appends_Runs
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add app shared tests
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Add runs load more"
  ```

### Task 9: Browser Verification for Restored Flows

**Files:**
- Modify: `tests/Lfm.E2E/Specs/ProfileSpec.cs`
- Modify: `tests/Lfm.E2E/Specs/RunsSpec.cs` or the existing runs E2E spec
- Modify: `tests/Lfm.E2E/Pages/GuildAdminPage.cs`
- Modify: `tests/Lfm.E2E/Pages/RunsPage.cs`

- [ ] **Step 1: Add E2E only where bUnit cannot prove the contract.**

  Add browser tests for:

  - dirty form navigation shows unsaved prompt and stay/leave choices work
  - site admin can load another guild by id on `/guild/admin`
  - signed-up raider can change attendance from the existing signup panel

  Do not add E2E tests for pure sorting, pagination, localization keys, mapper fields, or API validation already covered by unit/bUnit tests.

- [ ] **Step 2: Run E2E drift check and targeted E2E.**

  ```bash
  ./scripts/check-e2e-drift.sh
  dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release --filter "Unsaved|GuildAdmin|Signup"
  ```

  Expected: pass. If Docker is unavailable, stop and report that E2E verification is blocked by Docker.

- [ ] **Step 3: Commit.**

  ```bash
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add tests/Lfm.E2E
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Cover restored frontend flows"
  ```

### Task 10: Final Verification and Review

**Files:**
- No new files unless verification reveals a defect.

- [ ] **Step 1: Format C# after code changes.**

  ```bash
  dotnet format lfm.sln
  ```

- [ ] **Step 2: Run full relevant verification.**

  ```bash
  dotnet build lfm.sln -c Release
  dotnet test tests/Lfm.Api.Tests/Lfm.Api.Tests.csproj -c Release
  dotnet test tests/Lfm.App.Core.Tests/Lfm.App.Core.Tests.csproj -c Release
  dotnet test tests/Lfm.App.Tests/Lfm.App.Tests.csproj -c Release
  ./scripts/check-e2e-drift.sh
  dotnet test tests/Lfm.E2E/Lfm.E2E.csproj -c Release
  dotnet format lfm.sln --verify-no-changes --no-restore --severity error
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan diff --check
  ```

  Expected: all commands pass. If full E2E cannot run because Docker is unavailable, record the exact Docker error and keep the targeted bUnit/API evidence in the PR description.

- [ ] **Step 3: Request code review before merge.**

  Use `superpowers:requesting-code-review`. Review focus:

  - wire contract additions have live consumers
  - no PII or raw Blizzard payload leakage
  - restored flows remain accessible and keyboard-operable
  - no E2E assertions duplicate cheaper test lanes
  - site-admin update path uses site-admin authorization and audit logging

- [ ] **Step 4: Commit any verification fixes.**

  ```bash
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan status --short
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan add <changed-files>
  git -C /home/souroldgeezer/repos/lfm/.worktrees/typescript-frontend-parity-plan commit -m "Fix frontend parity verification"
  ```

## Suggested PR Breakdown

The full plan is larger than the repo's preferred branch size. Prefer this PR order:

1. Signup wire data plus run signup UI (`Task 1` and `Task 2`).
2. Character list state and unsaved guard (`Task 3` and `Task 4`).
3. Guild setup/home editor and site-admin editor (`Task 5` and `Task 6`).
4. Account menu and runs load more (`Task 7` and `Task 8`).
5. E2E coverage and final cleanup (`Task 9` and `Task 10`) if not included in the owning PRs.

Each PR should stay under the guidance thresholds where possible. If a task grows past the threshold, split it before broadening the change.

## Self-Review

- Spec coverage: covers old signup edit/change-character/spec/cancel flow, character sort/page/redirect, unsaved changes, guild setup guard, guild home editor, site-admin cross-guild admin, selected-character account menu, and runs pagination continuation.
- Intentional gaps: `/login/success` is excluded because current auth callback does not need it; old run manual sort is excluded because current horizon grouping is the better primary model; old React/MUI primitives are translated into Fluent UI and focused Blazor components.
- Placeholder scan: no placeholder tasks remain; every task has files, tests, commands, and expected outcomes.
- Type consistency: contract additions are named consistently across API, app-core, and app tasks: `CharacterSpecializationDto`, `CharacterDto.Specializations`, `RunCharacterDto.CharacterId`, `RunCharacterDto.SpecId`, `IGuildClient.GetAdminAsync`, and `IGuildClient.UpdateAdminAsync`.
