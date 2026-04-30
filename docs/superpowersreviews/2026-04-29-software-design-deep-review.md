# Software Design — Deep Review

- Date: 2026-04-29
- Mode: Review (deep, whole-repo)
- Skill: `souroldgeezer-design:software-design`
- Reference: `souroldgeezer-design/docs/software-reference/software-design.md`
- Extensions loaded: `dotnet`
- Stack signals: `.sln`, `.csproj`, `.cs`/`.razor`, `IServiceCollection`, `BackgroundService`-style timer functions, `InternalsVisibleTo`, `Directory.Build.props`, `global.json`
- Verification layers used: `static`, `graph` (project-reference graph derived from `.csproj`)
- Verification layers NOT used: `history` (git-churn), `runtime` (telemetry), `human` (team/ownership input). Severity reflects this.

## 1. Scope

Whole-repo design audit of [lfm.sln](../../lfm.sln). In scope: project graph, namespace/assembly boundaries, dependency direction, persistence/domain/contract layering, semantic coherence between stored documents and wire DTOs, Function-handler responsibility shape, App↔App.Core split, and razor-page form duplication.

Out of scope (delegated): HTTP contract correctness (`serverless-api-design`), responsive UI / WCAG / i18n (`responsive-design`), test-quality classification (`test-quality-audit`), security posture (`devsecops-audit`), ArchiMate / drift (`architecture-design`).

## 2. Project Assimilation Snapshot

Project graph (one-way, no cycles):

```
Lfm.Contracts ── (no deps)
  ▲
  ├── Lfm.Api ───────────────────────────┐
  ├── Lfm.App.Core ─────────────────┐    │
  └── Lfm.App ── Lfm.App.Core ──────┘    │
                                         │
Tests:  Lfm.Api.Tests        → Lfm.Api ──┘
        Lfm.App.Tests        → Lfm.App
        Lfm.App.Core.Tests   → Lfm.App.Core
        Lfm.E2E              → (built artifacts, no project ref)
```

Key conventions in place:

- `TreatWarningsAsErrors=true` and `Nullable=enable` everywhere.
- `RestoreLockedMode` enforced on CI for all projects except `Lfm.App` (Blazor WASM SDK constraint, documented in [app/Lfm.App.csproj](../../app/Lfm.App.csproj)).
- `InternalsVisibleTo` is narrow (`Lfm.Api.Tests` only) and used at two declared seams (`SiteAdminService`, `ReferenceSync`).
- `Lfm.App.Core` is a separate assembly with `RootNamespace=Lfm.App` so consuming razor markup still says `using Lfm.App.Services;` — deliberate split for Stryker mutation testing per [CLAUDE.md](../../CLAUDE.md) §Testing.

Overall layering is clean: dependency direction is inward toward contracts, contracts is the leaf, no cycles, and the App↔Api boundary is a wire-only translation (`Lfm.App.Core/Services/*Client.cs` ↔ `Lfm.Api/Functions/*Mapper.cs` ↔ `Lfm.Contracts.*Dto`). The findings below are within that healthy frame.

## 3. Findings

Findings sorted by severity then by family. Each cites evidence and the smallest useful correction. `block` is reserved for design risk that forces fragmentation or makes a likely change unsafe; this audit found none.

---

### `[SD-S-5]` [api/Repositories/IRaidersRepository.cs:14-32](../../api/Repositories/IRaidersRepository.cs#L14-L32) — Blizzard wire shapes are the persistence model

- bucket: semantic
- layer: static
- severity: warn
- evidence: `BlizzardRealmRef`, `BlizzardNamedRef`, `BlizzardAccountCharacter`, `BlizzardWowAccount`, `BlizzardAccountProfileSummary`, `BlizzardCharacterMediaAsset`, `BlizzardCharacterMediaSummary`, `BlizzardCharacterProfileResponse`, `BlizzardCharacterSpecializationsResponse` are declared inside the repository contract file with snake_case JSON property names (Blizzard's vocabulary) AND used as Cosmos document fragments AND threaded through service interfaces ([api/Services/IBlizzardProfileClient.cs:23,36](../../api/Services/IBlizzardProfileClient.cs#L23)) and Function handlers ([api/Functions/RaiderCharacterAddFunction.cs:128-149,203-235](../../api/Functions/RaiderCharacterAddFunction.cs#L128-L235)). The header comment on the file is explicit: "Blizzard account profile — stored verbatim as returned by the Blizzard API."
- action: introduce a translation seam between the Blizzard adapter (`Lfm.Api.Services.IBlizzardProfileClient`) and the persistence/domain shape. Two lower-cost moves before a full anti-corruption layer: (1) move the `Blizzard*` records out of `IRaidersRepository.cs` into a `Lfm.Api.Services.Blizzard.Models` namespace so the repository file describes only the persisted document, then (2) split the `*Summary` types stored in Cosmos from those returned by the HTTP client. The current shape skips reference §6.5 ("Anti-Corruption Boundary") on a justified-by-convenience basis; record the rejected abstraction explicitly with the cost (any Blizzard schema change forces a Cosmos schema migration).
- ref: software-design.md §3.3, §6.5; smell-catalog `SD-S-5`, `dotnet.SD-S-2`

---

### `[dotnet.SD-S-2]` [api/Repositories/IRunsRepository.cs:50-75](../../api/Repositories/IRunsRepository.cs#L50-L75) — `RunDocument` mixes Cosmos + working-domain roles with two serializers

- bucket: semantic
- layer: static
- severity: warn
- evidence: `RunDocument` carries Newtonsoft attributes (`[property: JsonConverter(typeof(LocalizedStringConverter))]` from `using Newtonsoft.Json`) for Cosmos round-trip AND `[property: System.Text.Json.Serialization.JsonPropertyName("_etag")]` for STJ wire/test serialization. The same record is constructed by [api/Functions/RunsCreateFunction.cs:173-191](../../api/Functions/RunsCreateFunction.cs#L173-L191), mutated via `with { ... }` by [api/Functions/RunsUpdateFunction.cs:284-302](../../api/Functions/RunsUpdateFunction.cs#L284-L302) and [api/Functions/RunsSignupFunction.cs:216](../../api/Functions/RunsSignupFunction.cs#L216), and projected to a wire DTO at the boundary ([api/Functions/RunResponseMapper.cs](../../api/Functions/RunResponseMapper.cs)). The wire is properly translated, so this is "two roles" (persistence + working domain), not three — but the dual-attribute coupling is a real maintenance tax.
- action: keep the wire/persistence split as is (it's the pragmatic choice at hobby scale). Address the dual-serializer cost only if Cosmos SDK is upgraded to a STJ-native release, or document the rejected abstraction (separate `RunEntity` for Cosmos and `Run` for domain) so future readers don't re-discover the choice.
- ref: software-design.md §3.3, §3.6; smell-catalog `dotnet.SD-S-2`, `SD-Q-1`

---

### `[SD-W-5]` [app/Pages/CreateRunPage.razor](../../app/Pages/CreateRunPage.razor) and [app/Pages/EditRunPage.razor](../../app/Pages/EditRunPage.razor) — Run-form state and behavior duplicated across two pages

- bucket: waste
- layer: static
- severity: warn
- evidence: both pages declare verbatim copies of `_expansionId`, `_activity`, `_instanceId`, `_difficulty`, `_size`, `_keystoneLevel`, `_anyDungeon`, `_startTimeLocal`, `_signupCloseLocal`, `_showSignupClose`, `_visibility`, `_description`, `_activityOptions`, `_dungeonScopeOptions`, `_difficultyOptions`, `_visibilityOptions`, `DescriptionAttrs`, `FilteredInstances`, `ShowInstanceDropdown`, `ShowDifficultyToggle`, `_canCreateGuildRuns`, `_canShowGuildOption`, plus the methods `OnActivityChanged`, `OnDungeonScopeChanged`, `OnInstanceChanged`, `OnDifficultyChanged`, `RebuildStaticOptions`, `RefreshDifficultyOptions`, `ResolveCurrentSeasonId`, `ToIsoOrNull`. [CreateRunPage.razor:385-426](../../app/Pages/CreateRunPage.razor#L385-L426) and [EditRunPage.razor:341-489](../../app/Pages/EditRunPage.razor#L341-L489) are near-identical. A change to a difficulty rule, a new visibility option, or a localized option label needs editing both files.
- action: extract a `RunFormState` class (or `RunFormController`) into [app/Lfm.App.Core/Runs/](../../app/Lfm.App.Core/Runs/) — the seam already exists alongside `ActivityKind`, `InstanceOptions`, `DifficultyLabel`, `RunTimeDefaults`. Pull the field cluster, derived properties, and option-rebuild helpers in. Optionally a `<RunForm/>` Blazor component holding the markup once the state class proves the boundary. This is the smallest move that retires the duplication without inventing a framework.
- ref: software-design.md §3.4, §6.3; smell-catalog `SD-W-5`, `SD-E-1`

---

### `[SD-E-1]` Same as above — likely-change radius is two pages

- bucket: evolution
- layer: static
- severity: warn
- evidence: any addition to the run-mode rules (e.g. a new `Difficulty`, a new visibility tier, an additional locked-field constraint) currently requires synchronized edits in `CreateRunPage.razor`, `EditRunPage.razor`, server-side `RunsCreateFunction`/`RunsUpdateFunction`, plus contract DTOs. Pages 2–4 of the diff are pure mechanical mirroring. The waste smell above (`SD-W-5`) names the duplication; this finding names the cost.
- action: same correction as `SD-W-5` collapses both findings.
- ref: software-design.md §3.5; smell-catalog `SD-E-1`

---

### `[SD-B-1]` [api/Functions/RunsUpdateFunction.cs](../../api/Functions/RunsUpdateFunction.cs) (378 lines) — Function handler aggregates too many reasons to change

- bucket: boundary
- layer: static
- severity: warn
- evidence: a single `RunsUpdateFunction.Run` performs HTTP method binding, If-Match parsing, principal extraction, raider lookup, GUILD permission check, manual `JsonDocument` parsing for "is field present vs explicitly null" detection ([RunsUpdateFunction.cs:111-157](../../api/Functions/RunsUpdateFunction.cs#L111-L157)), FluentValidation, run-editability rules, locked-field rules, GUILD visibility-promotion guard, instance lookup + `(instanceId, difficulty, size)` matching against the canonical instance list, mode-key derivation, optimistic-concurrency conflict translation, audit emission, and ETag echo. Reasons-to-change span at least four owners: transport, auth, run policy, and Blizzard reference data. Companions in the same shape: [RunsSignupFunction.cs:50-239](../../api/Functions/RunsSignupFunction.cs#L50-L239) (254 lines, owns retry loop + signup business rule + rejection logic), [RaiderCharacterAddFunction.cs](../../api/Functions/RaiderCharacterAddFunction.cs) (326 lines).
- action: two cheap moves. (1) lift the run-mutation policy into a `RunUpdateService` in [api/Services/](../../api/Services/) accepting `(RunDocument existing, UpdateRunRequest body, SessionPrincipal, IReadOnlyList<InstanceDto>) → Result<RunDocument, RunError>`. The Function adapter shrinks to: parse → call service → translate result to HTTP. (2) lift the `HasJsonProperty`/explicit-null detection into a small `JsonPatch`-style helper in [api/Helpers/](../../api/Helpers/). Do *not* introduce MediatR or CQRS pipelines — the existing scope does not justify it (`dotnet.SD-W-2`).
- ref: software-design.md §3.2, §3.4, §6.3; smell-catalog `SD-B-1`, `SD-B-5`, `dotnet.SD-B-4`

---

### `[SD-B-5]` [api/Functions/GuildMapper.cs:38-75](../../api/Functions/GuildMapper.cs#L38-L75) — `GuildDto` permission/editor fields hardcoded to false in the only mapper

- bucket: boundary
- layer: static
- severity: warn
- evidence: `GuildMapper.MapToDto` always returns `Settings: null`, `Editor: new GuildEditorDto(CanEdit: false)`, and `MemberPermissions: new GuildMemberPermissionsDto(false, false, false)`. Both [GuildFunction.GuildGet](../../api/Functions/GuildFunction.cs#L77) and [GuildAdminFunction.Run](../../api/Functions/GuildAdminFunction.cs#L43) return the mapper output directly without overriding these fields. Server-side permission checks then compute the *true* effective permissions inline at the Function ([RunsCreateFunction.cs:103](../../api/Functions/RunsCreateFunction.cs#L103), [RunsUpdateFunction.cs:100](../../api/Functions/RunsUpdateFunction.cs#L100), etc.) via `IGuildPermissions`. Meanwhile the SPA reads `_guild?.MemberPermissions?.CanCreateGuildRuns` to gate the visibility default ([CreateRunPage.razor:414-418](../../app/Pages/CreateRunPage.razor#L414-L418)) — so the client decision is always "false" even when the server would say yes.
- action: pass `IGuildPermissions` (and the loaded raider) into `GuildMapper.MapToDto`, or call `IGuildPermissions` from the Function and pass an already-evaluated permissions object to the mapper. This brings the wire payload in line with what the server actually authorizes and removes a contract slot that currently misleads the client. Confirm with a domain owner whether "false everywhere" is intentional WIP or oversight before changing — this audit cannot rule out an in-flight feature.
- ref: software-design.md §3.2, §3.3; smell-catalog `SD-B-5`, `SD-W-3`

---

### `[dotnet.SD-B-1]` [api/Functions/RunResponseMapper.cs](../../api/Functions/RunResponseMapper.cs) and [api/Functions/GuildMapper.cs](../../api/Functions/GuildMapper.cs) — Translation helpers under `Functions/`

- bucket: boundary
- layer: static
- severity: info
- evidence: both files live under `api/Functions/` namespace `Lfm.Api.Functions` even though they are pure projection helpers from a `*Document` to a `*Dto` and not Functions. Routing them through Functions blurs the folder rule established for the rest of the directory.
- action: move both to `api/Mappers/` (or `api/Functions/Mappers/`) under `namespace Lfm.Api.Mappers`. Pure rename; mechanical.
- ref: software-design.md §3.2; smell-catalog `dotnet.SD-B-1`, `SD-B-2`

---

### `[SD-C-3]` [api/Helpers/](../../api/Helpers/) — `Helpers` accreting unrelated responsibilities

- bucket: coupling
- layer: static
- severity: info
- evidence: `Problem.cs` (HTTP problem+json builder, transport concern), `InternalErrorResult.cs` (HTTP IActionResult), `RunEditability.cs` (run-domain policy: when is a run still editable?), `RunModeResolver.cs` (run-domain projection between legacy `ModeKey` and typed `Difficulty/Size`) all share the `Lfm.Api.Helpers` namespace. Two of these are transport, two are run-domain policy.
- action: move `RunEditability` and `RunModeResolver` into a `Lfm.Api.Runs` namespace (or merge into the proposed `RunUpdateService`). Keep `Problem` and `InternalErrorResult` as `Lfm.Api.Http`. Three small renames; no behavior change.
- ref: software-design.md §3.2, §3.4; smell-catalog `SD-C-3`, `SD-B-2`

---

### `[SD-E-4]` [api/Functions/RunsMigrateSchemaFunction.cs](../../api/Functions/RunsMigrateSchemaFunction.cs) + [api/Helpers/RunModeResolver.cs:19-28](../../api/Helpers/RunModeResolver.cs#L19-L28) — Legacy `ModeKey` strangler with stated retirement criterion

- bucket: evolution
- layer: static
- severity: info (downgraded — retirement criterion is documented)
- evidence: `RunDocument.ModeKey` is the legacy composite kept "for one cycle for cross-compatibility" ([IRunsRepository.cs:35-43](../../api/Repositories/IRunsRepository.cs#L35-L43)). `RunModeResolver.Resolve` falls back to parsing it; `RunsMigrateSchemaFunction` is the admin-triggered backfill. Without retirement, this would be a `warn` (`SD-E-4` Permanent strangler). The "one cycle" note + admin backfill function meet the reference §6.7 retirement-path requirement.
- action: track removal of `RunDocument.ModeKey` (and the `string ModeKey` field on the Cosmos write path) once the migrate function reports zero un-migrated documents in production. No change today; this finding exists to prevent the strangler from becoming permanent.
- ref: software-design.md §6.7; smell-catalog `SD-E-4`

---

### `[SD-Q-1]` [api/Lfm.Api.csproj:11-13,38-43](../../api/Lfm.Api.csproj#L11-L43) — Newtonsoft pinned for Cosmos round-trip without explicit cost line

- bucket: tradeoff
- layer: static
- severity: info
- evidence: the `.csproj` carries `AzureCosmosDisableNewtonsoftJsonCheck=true` and an explicit `Newtonsoft.Json 13.0.4` package reference, both with comments explaining "Cosmos SDK 3.x loads Newtonsoft.Json at runtime via DocumentClient even when the build-time check is disabled." The cost — every Cosmos document record must support both serializer attribute families ([IRunsRepository.cs:22-29](../../api/Repositories/IRunsRepository.cs#L22-L29), [IRaidersRepository.cs:42-44,75](../../api/Repositories/IRaidersRepository.cs#L42-L44), [IGuildRepository.cs:81-93](../../api/Repositories/IGuildRepository.cs#L81-L93)) — is real but unstated. The csproj describes the *workaround*, not the *cost*.
- action: add a one-line note to the csproj or [docs/storage-architecture.md](../../docs/storage-architecture.md) recording that document records carry dual serializer attributes by design, and the trigger to revisit (Cosmos SDK v4 with STJ default). No code change.
- ref: software-design.md §3.6; smell-catalog `SD-Q-1`

---

### `[SD-W-1]` [api/Repositories/SpecializationsRepository.cs](../../api/Repositories/SpecializationsRepository.cs), [api/Repositories/InstancesRepository.cs](../../api/Repositories/InstancesRepository.cs), [api/Repositories/ExpansionsRepository.cs](../../api/Repositories/ExpansionsRepository.cs) — Repository interfaces over reference data with one implementation each

- bucket: waste
- layer: static
- severity: info
- evidence: `IInstancesRepository`, `IExpansionsRepository`, `ISpecializationsRepository` are read-mostly repositories over blob-stored reference data that is also consumed via `IBlobReferenceClient`. Each interface has exactly one production implementation and one test usage path. They could be replaced by direct calls to `IBlobReferenceClient` plus thin projection helpers, but the seam is small and is being used uniformly through DI.
- action: keep as-is. Flagged only so that *new* reference-data accessors do not automatically sprout an `I*Repository` interface unless there is a real second implementation or a real test seam need. Adopt direct `BlobContainerClient` use plus a static projection function for the next reference type unless a need emerges.
- ref: software-design.md §3.1, §4.1; smell-catalog `SD-W-1`, `dotnet.SD-W-1`

---

### `[dotnet.SD-B-1]` [app/Lfm.App.Core/Lfm.App.Core.csproj:7](../../app/Lfm.App.Core/Lfm.App.Core.csproj#L7) — `RootNamespace=Lfm.App` on a separate assembly

- bucket: boundary
- layer: static
- severity: info (downgraded — rationale documented)
- evidence: `Lfm.App.Core` is a distinct assembly but uses `RootNamespace=Lfm.App`, identical to the parent `Lfm.App` project. Source files inside `Lfm.App.Core/Services/` declare `namespace Lfm.App.Services;`. Razor consumers in `Lfm.App` write `@using Lfm.App.Services;` and cannot tell from the namespace which assembly the type lives in. Default smell rule: namespace and assembly should converge. Rationale ([CLAUDE.md](../../CLAUDE.md) §Testing): Stryker.NET cannot mutate Blazor WASM (Razor source generators are not invoked during Stryker recompile), so framework-neutral code is hoisted into a sibling assembly and given the same root namespace to keep the consumer call sites unchanged.
- action: keep as-is. This finding exists so future readers do not "fix" the namespace and accidentally re-couple the assemblies. Consider a one-line `<!-- Why same RootNamespace -->` comment in the csproj pointing at CLAUDE.md.
- ref: software-design.md §3.7, §4.5; smell-catalog `dotnet.SD-B-1`

---

### `[SD-Q-2]` [api/Repositories/RunsRepository.cs:153-191](../../api/Repositories/RunsRepository.cs#L153-L191) — `ScrubRaiderAsync` does cross-partition query then full-document replace

- bucket: tradeoff
- layer: static (cost claim is hypothetical without runtime data)
- severity: info
- evidence: the GDPR raider-cleanup path issues a cross-partition `SELECT *` over the runs container then does sequential `ReplaceItemAsync` per modified run. At hobby scale this is fine and the comment says so ("TS uses `Promise.all`; sequential is fine at hobby scale"). The design choice is one-shot, scale-bounded.
- action: leave as-is. Track this only as a reminder that any future shift to higher-scale tier (>10k runs) needs a partial-update or patch-document operation here, not a full replace per run.
- ref: software-design.md §3.6; smell-catalog `SD-Q-2`

---

## 4. Rollup

| Severity | Count |
|---|---|
| block | 0 |
| warn  | 5 |
| info  | 7 |

Net design posture: clean dependency direction, no cycles, healthy contracts/persistence/wire layering, and a deliberate set of pragmatic tradeoffs (Newtonsoft pin, dual-attribute records, Blizzard verbatim cache, App↔App.Core namespace identity). The five `warn` findings cluster around two reasons-to-change:

1. **The Function handler is the de-facto domain layer.** Run/raider/guild policy, validation, optimistic-concurrency translation, and audit all live inside the Function class. This is a coherent vertical-slice choice but the largest handlers (`RunsUpdateFunction`, `RunsSignupFunction`, `RaiderCharacterAddFunction`) have grown past where the slice still pays for itself. Lift run-mutation policy to a thin service in [api/Services/](../../api/Services/) without introducing MediatR or CQRS scaffolding.
2. **Run form state lives twice.** `CreateRunPage` and `EditRunPage` carry a duplicated cluster of fields, derived properties, and helper methods. Extract `RunFormState` into [app/Lfm.App.Core/Runs/](../../app/Lfm.App.Core/Runs/) — the seam already exists alongside `InstanceOptions` and `RunVisualization`.

Two `warn` findings (`SD-S-5`, `SD-B-5`) call for explicit decisions rather than refactors: the Blizzard verbatim cache could be deliberate (current evidence is partial), and the `GuildDto` permission fields may be WIP.

## 5. Suggested Next Smallest Move

Pick one. The first is the highest leverage at the lowest cost.

1. **Extract `RunFormState`** into [app/Lfm.App.Core/Runs/RunFormState.cs](../../app/Lfm.App.Core/Runs/) and replace the duplicated field cluster in `CreateRunPage` and `EditRunPage` with delegating properties / event hooks. ~150 LOC moved, no behavior change, immediately shrinks the likely-change radius (`SD-W-5`, `SD-E-1`).
2. **Lift run-mutation policy** out of `RunsUpdateFunction` into a `RunUpdateService`. Keep the Function as a transport adapter (parse → call → translate). Companion change for `RunsCreateFunction` and `RunsSignupFunction` (`SD-B-1`, `SD-B-5`).
3. **Reach decision** on whether `GuildDto.MemberPermissions` and `Editor` should reflect `IGuildPermissions` output before changing anything else — talk to a domain owner first (`SD-B-5`).

## 6. Follow-up Status (2026-04-30)

Source-readable follow-up after the remediation branches landed on `main`.
This status did not add runtime, history, or human-ownership verification.

| Finding | Current status |
|---|---|
| `SD-B-5` guild DTO permissions | Resolved: `GuildMapper` now accepts `GuildEffectivePermissions`, and the guild functions compute effective permissions before mapping. |
| `dotnet.SD-B-1` mappers under `Functions/` | Resolved: pure projection helpers moved to `api/Mappers/`. |
| `SD-C-3` mixed helpers | Resolved: run-domain helpers moved to `api/Runs/`; HTTP helpers remain separate. |
| `SD-W-5` / `SD-E-1` run-form duplication | Resolved: shared `RunFormState` now owns form state, options, and stored-run hydration for create/edit pages. |
| `SD-B-1` Function handlers as domain layer | Resolved for the run mutation paths: create, update, and signup policy now sit behind `IRunCreateService`, `IRunUpdateService`, and `IRunSignupService`. |
| `SD-S-5` Blizzard wire shapes in persistence | Resolved: Blizzard HTTP wire records live under `api/Services/Blizzard/Models/`, with `BlizzardModelTranslator` converting them to stored shapes at the adapter boundary. |
| `dotnet.SD-S-2` / `SD-Q-1` Cosmos serializer tradeoff | Accepted and documented: Cosmos SDK 3.x keeps the Newtonsoft runtime pin; revisit when an STJ-default Cosmos SDK is available. |
| `SD-E-4` `ModeKey` strangler | Accepted with retirement criterion documented in `docs/storage-architecture.md`. |
| `SD-W-1` reference-data repositories | Accepted with guidance: prefer `IBlobReferenceClient` plus projection helpers for new blob-backed reference data unless a real second implementation or test seam exists. |
| `dotnet.SD-B-1` App.Core root namespace | Accepted and documented in `app/Lfm.App.Core/Lfm.App.Core.csproj`. |
| `SD-Q-2` raider scrub full replacements | Accepted as hobby-scale, one-shot cleanup design; revisit only if the run volume changes materially. |

## 7. Footer

```
Mode: Review (deep, whole-repo)
Extensions loaded: dotnet
Reference path: souroldgeezer-design/docs/software-reference/software-design.md
Verification layers used: static, graph
Verification layers NOT used: history (no git-churn analysis), runtime (no telemetry), human (no team/ownership input)
Project assimilation: 4 production projects + 4 test projects, no project cycles, dependency direction inward toward Lfm.Contracts; namespace conventions consistent; InternalsVisibleTo narrow and justified
Delegations: HTTP contract details → serverless-api-design; razor markup / WCAG / i18n → responsive-design; test scope and quality → test-quality-audit; security posture → devsecops-audit; ArchiMate / drift → architecture-design
Limits: severity reflects static evidence only; runtime, history, and human-ownership claims are out of scope for this audit
```
