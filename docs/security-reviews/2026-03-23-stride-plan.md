# STRIDE Report + Remediation Plan

## Summary

- Highest-risk confirmed finding: `Spoofing / Elevation` in the OAuth callback. Non-test callback handling in `functions/src/functions/battlenet-callback.ts` only rejects invalid `login_state` when the cookie exists; a missing cookie currently falls through to token exchange in `functions/src/lib/battlenet.ts`. Fix by failing closed unless the request is the explicit local test-mode path.
- Confirmed `Tampering / CSRF` issue: authenticated logout is a state-changing `GET` in `functions/src/functions/battlenet-logout.ts` while the session cookie is `SameSite=Lax`. External top-level navigations can clear sessions. `GET /battlenet/characters` also mixes read semantics with cache writes.
- Confirmed `Tampering` hardening gap: Blizzard character endpoints construct Blizzard URLs from unvalidated `region` / `realm` / `name` input in `functions/src/functions/raider-character.ts` and the portrait endpoint. Scope is limited to Blizzard hosts, but validation and path encoding are missing.
- Confirmed `Repudiation` gap: login/logout, raid mutations, signup changes, and account deletion do not emit structured actor/action logs.
- No critical direct information-disclosure bug was confirmed in raid responses. Guild-only raid access is enforced server-side and `raiderBattleNetId` is stripped from response payloads before returning them.

## Implementation Changes

- OAuth hardening:
  - Change non-test callback handling so `GET /api/battlenet/callback` rejects unless both a `login_state` cookie and matching `state` query param are present and valid.
  - Treat missing cookie, missing `state`, mismatch, expired cookie, and replay as the same failure path: clear `login_state`, skip token exchange, redirect to `/login/failed`.
  - Keep the deterministic callback bypass only behind the existing local test-mode guard; do not broaden that guard.
- Logout and read/write separation:
  - Replace `GET /api/battlenet/logout` with `POST /api/battlenet/logout`.
  - Return `200` JSON `{ "loggedOut": true, "redirectTo": "/login" }` and clear `battlenet_token` on that POST.
  - Replace the frontend anchor logout in `frontend/src/components/layout/NavBar.tsx` with an imperative API call plus client navigation to `/login`. Remove `getLogoutUrl()` and add a `logout()` helper.
  - Make `GET /api/battlenet/characters` read-only: return cached characters if present, otherwise `204 No Content` without persisting anything.
  - Keep `POST /api/battlenet/characters/refresh` as the only cache-writing path. On first load, the characters page calls `GET`; if it receives `204`, it immediately calls the existing refresh endpoint and renders that result.
- Input validation:
  - Add a shared validator for Blizzard path inputs: `region` must be one of `eu|us|kr|tw|cn`; `realm` and `name` must be non-empty normalized strings; every Blizzard path segment must use `encodeURIComponent`.
  - Apply that validator to character selection and portrait fetch endpoints before any Blizzard API call. Invalid input returns `400`.
  - Derive CORS allow-origin from `new URL(APP_BASE_URL).origin` instead of trusting the raw env string, while keeping the current single-origin policy.
- Repudiation coverage:
  - Add structured logs for login start, callback success/failure, logout, raid create/update/delete, signup/cancel, and account deletion.
  - Log action name, hashed/current `battleNetId`, target raid ID when relevant, and result status.
  - Never log access tokens, cookies, raw Blizzard IDs, or request bodies.
- Deferred in this pass:
  - The earlier `GET /api/guild/motd` idea is abandoned for this product path because Blizzard API support is not available; do not treat it as deferred implementation work.
  - Leave the frontend CSP `style-src 'unsafe-inline'` as documented technical debt, not part of the STRIDE remediation pass.

## Public API / Interface Changes

- `POST /api/battlenet/logout` replaces `GET /api/battlenet/logout`.
- `GET /api/battlenet/characters` becomes cache-only and may return `204 No Content` on cache miss.
- Frontend auth helper changes from URL generation to an imperative logout function.
- No response-shape changes for raid endpoints or OAuth success/failure redirects.

## Test Plan

- Backend tests:
  - Callback rejects missing `login_state`, missing `state`, mismatched `state`, expired cookie, and replay attempts; local test mode still succeeds.
  - Logout `POST` clears the cookie and `GET` is no longer accepted.
  - `GET /battlenet/characters` returns cached data without writing, returns `204` on cache miss, and `POST /battlenet/characters/refresh` remains the write path.
  - Invalid `region`, slash-containing `realm`, and malformed `name` are rejected before Blizzard fetches; valid inputs still reach encoded Blizzard URLs.
  - Structured logs are emitted for audited actions without leaking secrets.
- Frontend tests:
  - Navbar logout triggers `POST /battlenet/logout` and navigates to `/login`.
  - Characters page handles `204` cache miss by calling refresh once, then renders characters and preserves the current redirect flow after character selection.
- Regression checks:
  - Existing auth, character selection, raid CRUD, signup, and access-control tests continue to pass.

## Assumptions

- This STRIDE run is scoped to repo-visible application code and checked config, not external Azure edge or WAF settings.
- The existing local test-mode guard is intentionally retained as the only allowed state-bypass path.
- Cookie-domain sharing across `.dinosauruskeksi.com` is left unchanged in this pass; the remediation focuses on request semantics and handler behavior.
