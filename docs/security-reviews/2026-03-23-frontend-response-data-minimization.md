# Security Review: Frontend Response Data Minimization

- Date: 2026-03-23
- Scope: browser-hit routes between `frontend/` and `functions/`, with the requirement that the frontend only receives the minimum data needed for its current functionality
- Stack: React SPA, Azure Functions v4, TypeScript, cookie-backed Battle.net auth
- Method: Read-only code review against the current repository state

## Executive Summary

The frontend currently consumes a small set of backend routes, but several of those routes still return document-shaped payloads instead of route-specific view models. The highest-priority problem is a contract mismatch: raid responses intentionally strip `raiderBattleNetId`, while the signup UI still tries to use that field to identify the caller's own signup. That is already a correctness issue, and it blocks a clean data-minimization pass if left unresolved.

The main minimization gaps are concentrated in four routes:

- `POST /api/raids` returns the full stored raid document even though the frontend only uses the new raid id.
- `GET /api/raider/characters` and `POST /api/raider/character` return broader character objects than the current UI needs.
- `GET /api/raids` and the signup mutation responses return near-document-shaped raid payloads containing creator metadata and roster fields that are not rendered by the current frontend.

Some routes are already appropriately minimal for current use:

- `POST /api/battlenet/character-portraits` returns only the requested portrait map.
- `GET /api/battlenet/login`, `GET /api/battlenet/callback`, and `GET /api/battlenet/logout` are redirect-oriented and do not expose excess JSON response data.

## Routes Reviewed

- `GET /api/me`
- `GET /api/instances`
- `GET /api/raids`
- `POST /api/raids`
- `POST /api/raids/{id}/signup`
- `DELETE /api/raids/{id}/signup`
- `GET /api/raider/characters`
- `POST /api/raider/character`
- `GET /api/battlenet/characters`
- `POST /api/battlenet/character-portraits`
- `GET /api/battlenet/login`
- `GET /api/battlenet/callback`
- `GET /api/battlenet/logout`

## High Severity

### 1. Raid signup UI relies on an identifier the backend intentionally strips

- Severity: High
- Category: Contract integrity / data minimization blocker
- Location:
  - `functions/src/lib/raidResponseSanitizer.ts:17-24`
  - `functions/src/lib/raidResponseSanitizer.test.ts:70-74`
  - `frontend/src/features/raids/components/RaidSignupCard.tsx:49-52`
- Evidence:
  - Raid responses are sanitized to remove `raiderBattleNetId`.
  - The frontend still searches `raid.raidCharacters` by `raiderBattleNetId === user.battleNetId` to determine the caller's existing signup.
- Impact:
  - The caller's existing signup cannot be derived reliably from the actual API contract.
  - Current-user actions such as canceling signup, preserving selected character/spec, and showing existing status are fragile or incorrect.
  - Re-adding raw Battle.net ids to the response would work against the minimization goal.
- Fix:
  - Keep `raiderBattleNetId` out of browser responses.
  - Add explicit caller-scoped signup metadata to raid responses, for example `currentUserSignup { characterId, specId, desiredAttendance }`, or another equivalent route-specific shape.

## Medium Severity

### 2. `POST /api/raids` returns the full stored raid document although the frontend only uses `id`

- Severity: Medium
- Category: Excess response data
- Location:
  - `functions/src/functions/raids-create.ts:77-98`
  - `functions/src/functions/raids-create.ts:128-131`
  - `functions/src/types/index.ts:98-112`
  - `frontend/src/features/raids/pages/CreateRaidPage.tsx:139-148`
- Evidence:
  - The backend creates a full `RaidDocument`, including fields such as `creatorGuildId`, `creatorBattleNetId`, and `ttl`.
  - The handler returns the created resource directly.
  - The frontend only reads `res.data.id` and immediately navigates to `/raids?raid=<id>`.
- Impact:
  - Creator metadata and internal persistence fields are exposed to the browser without a frontend use case.
  - The route contract is broader than necessary and harder to lock down.
- Fix:
  - Return `{ id }` only.

### 3. Character selection routes over-return relative to current frontend needs

- Severity: Medium
- Category: Excess response data
- Location:
  - `functions/src/functions/raider-character.ts:120-123`
  - `functions/src/functions/raider-character.ts:143-146`
  - `functions/src/lib/blizzard-adapters.ts:172-191`
  - `frontend/src/features/characters/pages/CharactersPage.tsx:93-100`
  - `frontend/src/features/raids/pages/RaidsPage.tsx:109-114`
  - `frontend/src/components/layout/AppLayout.tsx:28-34`
  - `frontend/src/features/raids/components/RaidSignupCard.tsx:59-87`
- Evidence:
  - `POST /api/raider/character` returns `{ character, selectedCharacterId }`, but the frontend only uses `selectedCharacterId`.
  - `GET /api/raider/characters` returns full `toSelectedCharacterView()` objects.
  - The current frontend only needs a subset:
    - App layout: selected character `id`, `name`, `portraitUrl`
    - Raid signup: `id`, `name`, `realm`, `activeSpecId`, `specializations[{ id, name }]`, and `selectedCharacterId`
- Fields currently returned but not used by the frontend:
  - `region`
  - `level`
  - `classId`
  - `raceId`
  - `fetchedAt`
  - `specializations[].role`
- Impact:
  - Extra character profile metadata is exposed to the browser without a current product need.
  - The response shape is tightly coupled to storage and Blizzard adapter internals instead of the UI's actual requirements.
- Fix:
  - `POST /api/raider/character`: return `{ selectedCharacterId }` only.
  - `GET /api/raider/characters`: return a route-specific DTO containing only `selectedCharacterId` plus the character fields the current UI reads.

### 4. Raid list and signup mutation responses expose unnecessary raid and roster fields

- Severity: Medium
- Category: Excess response data
- Location:
  - `functions/src/functions/raids-list.ts:25-26`
  - `functions/src/functions/raids-signup.ts:108-114`
  - `functions/src/functions/raids-cancel-signup.ts:35-40`
  - `functions/src/lib/raidResponseSanitizer.ts:26-31`
  - `frontend/src/features/raids/pages/RaidsPage.tsx:267-316`
  - `frontend/src/features/raids/components/RaidInfoCard.tsx:21-49`
  - `frontend/src/features/raids/components/RaidSummaryItem.tsx:21-55`
  - `frontend/src/features/raids/components/RosterSection.tsx:32-40`
- Evidence:
  - Raid responses are built from near-complete `RaidDocument` objects.
  - The current frontend uses:
    - top-level: `id`, `instanceId`, `instanceName`, `modeKey`, `startTime`, `signupCloseTime`, `description`
    - roster display: `id`, `characterName`, `characterClassId`, `characterClassName`, `specName`, `desiredAttendance`, `role`
    - caller-specific signup state: logically needs `characterId` and `specId`, but only for the current caller's signup
- Fields currently returned but not used in the current raid UI:
  - top-level: `visibility`, `creatorBattleNetId`, `creatorGuild`, `createdAt`
  - per-signup: `characterRealm`, `characterLevel`, `characterRaceId`, `characterRaceName`, `reviewedAttendance`
- Additional note:
  - `characterId` and `specId` do not need to be exposed on every roster entry. They are only useful for the caller's own signup state.
- Impact:
  - Creator metadata and unnecessary roster attributes are pushed to every authenticated browser session that can view the raid.
  - The browser contract is wider than the UI behavior requires, increasing coupling and review surface.
- Fix:
  - Define a route-specific raid DTO for list and signup-update responses.
  - Keep shared roster entries minimal.
  - Move caller-only state into a separate `currentUserSignup` field instead of exposing identifiers on all signups.

## Low Severity

### 5. Several supporting routes have smaller trimming opportunities

- Severity: Low
- Category: Excess response data
- Location:
  - `functions/src/functions/me.ts:13-17`
  - `frontend/src/lib/auth.ts:3-12`
  - `functions/src/functions/battlenet-characters.ts:21-33`
  - `functions/src/lib/blizzard-adapters.ts:122-133`
  - `frontend/src/features/characters/pages/CharactersPage.tsx:135-179`
  - `functions/src/functions/instances-list.ts:10-13`
  - `functions/src/lib/blizzard-adapters.ts:83-96`
  - `frontend/src/features/raids/pages/CreateRaidPage.tsx:75-90`
  - `frontend/src/lib/wow/instances.ts:3-18`
- Evidence:
  - `GET /api/me` returns `guildName`, while the current frontend state handling only needs `battleNetId` and `selectedCharacterId`.
  - `GET /api/battlenet/characters` returns `activeSpecId`, but the current character selection UI does not read it.
  - `GET /api/instances` returns `type`, `minLevel`, and `modes[].is_tracked`, while the current frontend only uses:
    - `id`
    - `name`
    - `expansionId`
    - `modes[].mode.type`
    - `modes[].mode.name`
    - `modes[].players`
- Impact:
  - These are smaller exposures than the raid and selected-character routes, but they still widen the browser contract unnecessarily.
- Fix:
  - Trim `GET /api/me` to `{ battleNetId, selectedCharacterId }`.
  - Drop `activeSpecId` from `GET /api/battlenet/characters` unless a frontend use case is added.
  - Trim `GET /api/instances` to the fields currently used by raid creation and mode-label resolution.

## Positive Controls

- Raid responses already avoid exposing `raiderBattleNetId` directly:
  - `functions/src/lib/raidResponseSanitizer.ts:17-24`
- Portrait lookup is already narrowly shaped to the frontend need:
  - `functions/src/functions/battlenet-character-portraits.ts:18-73`
- Battle.net auth entry/callback/logout routes are redirect-oriented and do not return broad JSON payloads:
  - `functions/src/functions/battlenet-login.ts:6-9`
  - `functions/src/functions/battlenet-callback.ts:12-39`
  - `functions/src/functions/battlenet-logout.ts:8-22`

## Recommended Remediation Order

1. Define an explicit raid response DTO for list and signup-mutation routes, and remove the frontend's dependency on `raiderBattleNetId`.
2. Change `POST /api/raids` to return `{ id }` only.
3. Slim `POST /api/raider/character` and `GET /api/raider/characters` to route-specific view models.
4. Trim `GET /api/me`, `GET /api/battlenet/characters`, and `GET /api/instances`.
5. Add contract tests that assert exact response keys for each frontend-hit route.
6. Add frontend coverage for existing-signup, cancel-signup, and change-character flows against the trimmed raid DTO.

## Reviewer Notes

This review was code-only. I did not capture runtime HTTP traffic or inspect browser devtools payloads in a live session. The findings are based on static comparison of backend response builders and the fields the current frontend actually reads.

The Battle.net login/logout/callback routes and portrait route already look appropriately minimal for their current responsibilities. The main review target is the JSON contract between raid and character routes and the browser.
