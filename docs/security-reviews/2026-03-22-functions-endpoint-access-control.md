# Security Review: Functions Endpoint Access Control

- Date: 2026-03-22
- Scope: `functions/` endpoint authentication and object-level authorization, with supporting frontend/auth-flow code where it directly affects endpoint security
- Stack: Azure Functions v4, TypeScript, cookie-backed Battle.net auth, React frontend
- Method: Read-only code review against the current repository state

## Executive Summary

The raid handlers themselves are mostly consistent: mutating raid routes require authentication, creator-only operations are enforced for update/delete, and guild-only raid reads/signups check `creatorGuildId` against the caller's derived `guildId`. The highest-risk issue is earlier in the trust chain: `/api/raider/character` does not enforce that the selected character actually belongs to the authenticated Battle.net account before using that character's guild to authorize later raid access. That can undermine the guild-based access checks across multiple endpoints.

Two additional auth-flow issues are present: OAuth `state` is signed but not bound to the user's pre-auth session, enabling login CSRF/session swapping, and redirect validation allows protocol-relative paths such as `//evil.example`, creating a post-auth open redirect.

## High Severity

### 1. Server-side character ownership is not enforced before guild-based authorization is derived

- Severity: High
- Rule IDs: EXPRESS-INPUT-001, Broken Access Control
- Location:
  - `functions/src/functions/raider-character.ts:45-48`
  - `functions/src/functions/raider-character.ts:75-105`
  - `functions/src/functions/raider-character.ts:115-123`
  - `functions/src/lib/blizzard-adapters.ts:116-125`
  - `functions/src/functions/raids-list.ts:12-22`
  - `functions/src/functions/raids-detail.ts:19-22`
  - `functions/src/functions/raids-signup.ts:77-80`
  - `functions/src/functions/raids-cancel-signup.ts:21-24`
  - `functions/src/functions/raids-create.ts:117-123`
- Evidence:
  - `/api/raider/character` accepts arbitrary `region`, `realm`, and `name` from the caller and fetches that character profile directly.
  - The handler persists the fetched character and sets it as `selectedCharacterId` without checking it against the authenticated account's own character list.
  - `toBattleNetIdentity()` then derives `guildId` from the selected character's profile.
  - Raid visibility checks trust that derived `guildId` for guild-scoped reads and signups, and raid creation trusts it for `GUILD` raids.
- Impact:
  - A caller can attempt to bind any fetchable character profile to their account and inherit that character's guild for authorization decisions.
  - If Blizzard's character profile endpoint is readable for non-owned characters, this becomes a direct access-control bypass for guild-scoped raids and guild-scoped raid creation.
- Fix:
  - Enforce server-side ownership in `/api/raider/character`.
  - Resolve the authenticated account's character inventory from `/profile/user/wow` or cached `accountProfileSummary`, then reject selections that are not present on that account before persisting `selectedCharacterId`.
  - Treat UI filtering as convenience only; authorization must be enforced in the function handler.
- Mitigation:
  - Until fixed, avoid treating `selectedCharacterId` as a trustworthy authorization source for guild membership.
  - Consider storing an account-verified character identifier and deriving guild membership only from previously verified account-owned characters.
- False positive notes:
  - If Blizzard rejects non-owned character profile reads for this token/scope combination, the exploit path may be blocked externally. The code does not enforce that assumption itself, so it should still be verified and hardened server-side.

## Medium Severity

### 2. OAuth state is signed but not bound to the requester's session

- Severity: Medium
- Rule IDs: Authentication Flow / CSRF
- Location:
  - `functions/src/functions/battlenet-login.ts:6-9`
  - `functions/src/lib/battlenet.ts:108-129`
  - `functions/src/lib/battlenet.ts:358-365`
  - `functions/src/functions/battlenet-callback.ts:14-43`
- Evidence:
  - Login initiation is anonymous and creates a signed `state` that only contains the redirect path.
  - Callback processing verifies the HMAC signature but does not compare `state` to a nonce stored in a cookie or server-side session.
  - Any callback with a valid OAuth `code` plus any previously generated signed `state` can set the `battlenet_token` cookie.
- Impact:
  - An attacker can complete OAuth on their own account and then trick a victim into visiting the callback URL, causing the victim's browser to establish a session for the attacker's account.
  - This is a login CSRF/session-swapping issue and can cause confused-deputy behavior and account mix-ups during subsequent state-changing actions.
- Fix:
  - Generate a high-entropy login nonce, store it in an `HttpOnly` cookie or server-side state store, include it in `state`, and require an exact match at callback before issuing the session cookie.
  - Clear the nonce after a single successful callback.
- Mitigation:
  - If server-side state is undesirable, at minimum bind the login flow to a nonce cookie and reject callbacks without a matching nonce.
- False positive notes:
  - HMAC-signing `state` protects integrity, but it does not provide CSRF protection unless the value is also bound to the initiating browser session.

### 3. Redirect validation allows protocol-relative targets in the auth flow

- Severity: Medium
- Rule IDs: EXPRESS-REDIRECT-001
- Location:
  - `functions/src/lib/battlenet.ts:75-79`
  - `functions/src/lib/battlenet.ts:168-171`
  - `frontend/src/features/auth/pages/LoginPage.tsx:7-8`
  - `frontend/src/features/auth/pages/LoginSuccessPage.tsx:10-12`
  - `frontend/src/features/characters/pages/CharactersPage.tsx:23-26`
  - `frontend/src/features/characters/pages/CharactersPage.tsx:43`
- Evidence:
  - `normalizeRedirectPath()` accepts any string beginning with `/`.
  - That allows protocol-relative values such as `//evil.example`.
  - The validated value is propagated through the OAuth flow and later passed to client-side navigation after login or character selection.
- Impact:
  - A user can be redirected to an attacker-controlled origin immediately after authentication, which is a practical phishing and session-confusion vector.
- Fix:
  - Only allow app-internal relative paths that start with a single `/`.
  - Explicitly reject `//`, backslashes, control characters, and absolute URLs.
  - Prefer a short allowlist of known application routes if possible.
- Mitigation:
  - Normalize invalid redirect inputs to a fixed safe default such as `/raids`.
- False positive notes:
  - This is not theoretical: standard URL resolution treats `//host` as a cross-origin network-path reference.

## Low Severity

### 4. Logout is a state-changing GET with no CSRF protection

- Severity: Low
- Rule IDs: CSRF / safe HTTP semantics
- Location:
  - `functions/src/functions/battlenet-logout.ts:8-22`
  - `functions/src/functions/battlenet-logout.ts:25-30`
- Evidence:
  - Logout clears the session cookie on a `GET /api/battlenet/logout`.
  - There is no anti-CSRF token, origin check, or requirement for a non-GET method.
- Impact:
  - A victim can be forced to log out via top-level navigation or a crafted link.
- Fix:
  - Change logout to `POST` and apply the same CSRF/session-binding pattern used for other cookie-authenticated state changes.
- Mitigation:
  - If the route must remain simple, require a same-origin `Origin`/`Referer` check before clearing the session.

## Positive Controls

- Raid mutation endpoints require authentication before any data access:
  - `functions/src/functions/raids-create.ts:97-99`
  - `functions/src/functions/raids-update.ts:79-81`
  - `functions/src/functions/raids-delete.ts:7-9`
  - `functions/src/functions/raids-signup.ts:23-25`
  - `functions/src/functions/raids-cancel-signup.ts:10-12`
- Creator-only authorization is enforced for raid update/delete:
  - `functions/src/functions/raids-update.ts:87-91`
  - `functions/src/functions/raids-delete.ts:15-19`
- Guild-only raid reads and signups do apply object-level checks once `guildId` is derived:
  - `functions/src/functions/raids-detail.ts:19-22`
  - `functions/src/functions/raids-signup.ts:77-80`
  - `functions/src/functions/raids-cancel-signup.ts:21-24`
- Administrative reference-data sync is not anonymous:
  - `functions/src/functions/wow-update.ts:16-20`

## Recommended Remediation Order

1. Fix `/api/raider/character` so guild identity can only be derived from account-owned characters.
2. Bind OAuth `state` to the initiating browser session before issuing the auth cookie.
3. Harden redirect validation to a strict internal-path allowlist.
4. Convert logout to `POST` and add a same-origin/CSRF check.

## Reviewer Notes

This review was code-only. I did not exercise the flows against live Battle.net services in this turn, so the first finding's exploitability depends on whether Blizzard allows the current token/scope to read non-owned character profiles. The application should not rely on that external behavior for authorization.
