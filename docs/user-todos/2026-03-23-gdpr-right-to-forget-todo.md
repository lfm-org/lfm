# GDPR Right-To-Forget Todo

## Current state

- Backend code for account deletion already exists in [functions/src/functions/me-delete.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/functions/me-delete.ts).
- That endpoint is not currently active because [functions/src/index.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/index.ts) does not import `./functions/me-delete.js`.
- The current deletion behavior deletes the raider document, removes the user's raid signups, nulls `creatorBattleNetId`, and clears the auth cookie.
- The frontend currently has no account settings page, no delete-account control, and no e2e coverage for account deletion.
- Existing backend unit coverage for `scrubRaidDocument` passes in [functions/src/functions/me-delete.test.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/functions/me-delete.test.ts).

## Findings

### 1. The backend endpoint exists but is not registered

The repo already contains the `me-delete` function implementation, but the Azure Functions barrel file never imports it, so the route is not actually available at runtime.

### 2. Current deletion semantics are weaker than the desired product behavior

The existing implementation removes the user's account record and participation links, but it leaves created raid records intact without anonymizing creator-authored fields like `description` and `creatorGuild`.

That is not aligned with the chosen behavior for this feature: keep raids, but anonymize created raids.

### 3. The frontend has no account-management surface

There is currently no settings route or authenticated page where a user can trigger permanent deletion. The current authenticated UI only exposes raids, character selection, and logout.

### 4. Deletion changes existing raid ownership behavior

Because `creatorBattleNetId` is nulled on deletion, the deleted user can no longer update or delete raids they originally created. That behavior is already implied by the backend and should remain consistent after the anonymization change.

### 5. Verification coverage is incomplete end to end

Backend unit tests exist for raid scrubbing logic, but there is no frontend unit-test runner and no Playwright coverage for the deletion flow. The feature needs browser coverage to protect the full user journey.

## User todos

- Register `me-delete` in [functions/src/index.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/index.ts) so `DELETE /api/me` is actually exposed.
- Update [functions/src/functions/me-delete.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/functions/me-delete.ts) so deleting an account anonymizes creator-authored raid fields instead of only unlinking the creator.
- Keep the current signup-removal and auth-cookie-clearing behavior.
- Add backend tests in [functions/src/functions/me-delete.test.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/functions/me-delete.test.ts) for creator-owned raid anonymization and mixed creator-plus-signup cases.
- Add a dedicated authenticated `/settings` route in [frontend/src/App.tsx](/home/souroldgeezer/repos/sisu-raidcal/frontend/src/App.tsx).
- Add a settings page with destructive-action copy, typed confirmation, loading/error states, and a final delete action.
- Add a navigation entry for settings from the authenticated shell, most likely in [frontend/src/components/layout/NavBar.tsx](/home/souroldgeezer/repos/sisu-raidcal/frontend/src/components/layout/NavBar.tsx).
- Add a frontend delete-account helper and auth-state reset path so successful deletion immediately transitions the app to logged-out state.
- Add Playwright coverage for the delete flow, including typed confirmation, success redirect, and protected-route behavior after deletion.
- Run backend tests and frontend build as the minimum verification set, then run targeted Playwright coverage for the new settings flow.

## Suggested implementation defaults

- Work in an isolated git worktree under `.worktrees/gdpr-right-to-forget`.
- Use branch name `feat/gdpr-right-to-forget`.
- Default anonymized replacements:
  - `description`: `Deleted by user`
  - `creatorGuild`: `Deleted account`
- Keep raid timing, instance, visibility, and non-user attendance data unchanged.
