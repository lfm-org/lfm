# GDPR Right-To-Forget Status

## Current repo state

- `DELETE /api/me` is live through [functions/src/index.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/index.ts) and implemented in [functions/src/functions/me-delete.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/functions/me-delete.ts).
- Shared raid-scrubbing behavior now lives in [functions/src/lib/raider-cleanup.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/lib/raider-cleanup.ts) with coverage in [functions/src/lib/raider-cleanup.test.ts](/home/souroldgeezer/repos/sisu-raidcal/functions/src/lib/raider-cleanup.test.ts).
- The authenticated delete-account entry point now lives on [frontend/src/features/characters/pages/CharactersPage.tsx](/home/souroldgeezer/repos/sisu-raidcal/frontend/src/features/characters/pages/CharactersPage.tsx) as a typed-confirmation `Forget me` action.
- Successful deletion clears frontend auth state and routes to public [frontend/src/features/auth/pages/GoodbyePage.tsx](/home/souroldgeezer/repos/sisu-raidcal/frontend/src/features/auth/pages/GoodbyePage.tsx).
- Browser coverage for the flow now lives in [frontend/e2e/account-delete.spec.ts](/home/souroldgeezer/repos/sisu-raidcal/frontend/e2e/account-delete.spec.ts).

## Notes

- The current backend behavior removes the raider document, removes raid signups, nulls creator ownership, and clears the auth cookie. This document does not reopen those semantics.
- The frontend entry point was intentionally added to `/characters`, not a separate `/settings` route.
- Deleted users lose edit/delete authority over raids they previously created because creator ownership is removed during cleanup.

## Follow-up

- If product requirements later change toward creator-field anonymization rather than ownership removal, track that as a new behavior change instead of treating it as unfinished work from this doc.
