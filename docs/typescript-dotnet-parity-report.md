# TypeScript-to-.NET Feature Parity Report

**Date:** 2026-04-09
**Baseline:** Git tag `the-last-typescript` (React + Node.js Azure Functions)
**Current:** .NET 10 Blazor WASM + Azure Functions Isolated Worker

---

## Executive Summary

The .NET port reproduces the core functionality of the TypeScript implementation: OAuth authentication, raid/run management, character syncing, guild management, and reference data. The backend API is feature-complete with minor structural changes. The frontend covers all primary workflows but lacks several polish features. Six cross-cutting concerns from the TypeScript version are absent.

**Verdict:** The .NET port is functionally equivalent for all primary user workflows. The gaps are in hardening (rate limiting, security headers, concurrency control), UI polish (theming, class colors, toast notifications), and internationalization.

---

## Terminology

The .NET port renames "raids" to "runs" throughout routes, repositories, and UI. This is an intentional rename, not a missing feature. This report uses "runs/raids" interchangeably when discussing equivalent concepts.

---

## 1. API Endpoints

### Fully Ported

| TypeScript Endpoint | .NET Endpoint | Notes |
|---|---|---|
| `GET /api/battlenet/login` | `GET /api/battlenet/login` | Identical PKCE flow |
| `GET /api/battlenet/callback` | `GET /api/battlenet/callback` | Identical token exchange + session cookie |
| `GET /api/battlenet/logout` | `POST /api/battlenet/logout` | Method changed GET→POST (improvement) |
| `GET /api/me` | `GET /api/me` | Same response shape |
| `POST /api/me/update` | `PATCH /api/me` | Method changed POST→PATCH; locale only (see below) |
| `POST /api/me/delete` | `DELETE /api/me` | Method changed POST→DELETE (improvement) |
| `GET /api/battlenet/characters` | `GET /api/battlenet/characters` | Same 15-min cooldown cache (was 5 min in TS) |
| `POST /api/battlenet/characters/refresh` | `POST /api/battlenet/characters/refresh` | Same enrichment (specs, portraits) |
| `POST /api/battlenet/character-portraits` | `POST /api/battlenet/character-portraits` | Same batch portrait resolution |
| `GET /api/raids` | `GET /api/runs` | Same visibility rules (PUBLIC/GUILD/creator) |
| `GET /api/raids/:id` | `GET /api/runs/{id}` | Same detail with roster |
| `POST /api/raids` | `POST /api/runs` | Same create with TTL (startTime + 7 days) |
| `PUT /api/raids/:id` | `PUT /api/runs/{id}` | Same edit with permission checks |
| `DELETE /api/raids/:id` | `DELETE /api/runs/{id}` | Same delete with permission checks |
| `POST /api/raids/:id/signup` | `POST /api/runs/{id}/signup` | Same signup (character + attendance + spec) |
| `POST /api/raids/:id/cancel-signup` | `DELETE /api/runs/{id}/signup` | Method changed POST→DELETE (improvement) |
| `GET /api/guild` | `GET /api/guild` | Same profile + roster + permissions |
| `PUT /api/guild` | `PATCH /api/guild` | Method changed PUT→PATCH |
| `GET /api/instances/list` | `GET /api/instances` | Route simplified; flat list vs hierarchical (see below) |
| `GET /api/health` | `GET /api/health` | Same static liveness check |
| `GET /api/privacy-contact` | `GET /api/privacy-contact/email` | Route slightly different; same response |
| `OPTIONS /api/*` | `OPTIONS /api/*` | CORS preflight handled |

### New in .NET (Not in TypeScript)

| Endpoint | Purpose |
|---|---|
| `GET /api/health/ready` | Readiness probe that validates Cosmos connectivity |
| `GET /api/reference/specializations` | Dedicated specialization list endpoint (TS loaded from blob) |
| `PUT /api/raider/characters/{id}` | Separate character selection (TS combined with add) |
| `POST /api/privacy/contact` | Privacy contact form submission (logs to App Insights) |
| `GET /api/e2e/login` | E2E test login (replaces TS test mode query params) |

### Missing from .NET

| TypeScript Endpoint | Impact | Notes |
|---|---|---|
| `GET /api/guild/crest` | **Low** | TS served crest images via redirect to blob storage. .NET returns crest URLs in guild response; frontend loads directly. Functionally equivalent. |
| `GET /api/guild/admin?guildSlug=X` | **Low** | Admin view of any guild by slug. Not present in .NET. Current guild admin page operates on the authenticated user's guild only. |
| `POST /api/wow/update` | **Present** | Exists in .NET with same admin-only access. |

### Behavioral Differences

| Behavior | TypeScript | .NET | Impact |
|---|---|---|---|
| Character selection | Part of `POST /api/raider/character` (add + select combined) | Separate `PUT /api/raider/characters/{id}` (select only, character must pre-exist) | **Low** — multi-step but same result |
| Me update fields | `selectedCharacterId` + `locale` | `locale` only | **Medium** — character selection moved to dedicated endpoint, but `MeClient` in frontend is missing `UpdateAsync` method for locale |
| Instance data structure | Hierarchical (instance → modes[]) | Flat (one document per instance-mode pair) | **Low** — same data, different shape |
| Reference data storage | Blob storage (JSON files) | Cosmos containers (`instances`, `specializations`) | **Low** — architectural improvement |
| Character cache cooldown | 5 minutes | 15 minutes | **Low** — slightly longer cooldown |
| Raider TTL | Not set explicitly | 180 days per document | **Low** — improvement for data hygiene |

---

## 2. Database

### Cosmos Containers

| Container | TypeScript | .NET | Notes |
|---|---|---|---|
| `raiders` | Yes (pk: `/battleNetId`) | Yes (pk: `/battleNetId`) | Same schema, .NET adds 180-day TTL |
| `raids` / `runs` | Yes (pk: `/id`) | Yes (pk: `/id`) | Same schema, renamed |
| `guilds` | Yes (pk: `/id`) | Yes (pk: `/id`) | Same schema |
| `instances` | No (blob storage) | Yes (pk: `/id`) | New container |
| `specializations` | No (blob storage) | Yes (pk: `/id`) | New container |
| `migrations` | Yes (umzug tracking) | No | Removed; Bicep handles container setup |

### Document Schemas

Document schemas are compatible. The .NET port preserves existing Cosmos data without migration. Key differences:

- `RaidDocument` → `RunDocument`: field names preserved, `raidCharacters` → `runCharacters`
- `RaiderDocument`: gains explicit 180-day TTL, otherwise identical
- `GuildDocument`: crest stored as separate emblem/border URLs (TS had single `crestUrl`)
- Both track `desiredAttendance` and `reviewedAttendance` on signup entries

### Indexing

Both versions use composite indexes for visibility queries. The .NET version adds a secondary index on `/lastSeenAt` for the cleanup timer query.

---

## 3. Authentication & Authorization

### OAuth 2.0 PKCE Flow

Fully ported. Both implementations follow the same flow:

1. Generate PKCE code verifier + challenge
2. Store state in `login_state` cookie (encrypted)
3. Redirect to Battle.net authorize
4. Exchange code for access token on callback
5. Create/update raider document
6. Encrypt session into `battlenet_token` cookie

**Encryption change:** TypeScript used AES-256-GCM + HMAC with raw keys (`SESSION_ENCRYPTION_KEY`, `HMAC_SECRET`). .NET uses ASP.NET Core Data Protection API with Key Vault key wrapping. Existing TypeScript session cookies cannot be decrypted by .NET — all users must re-login after cutover.

### Authorization

| Check | TypeScript | .NET |
|---|---|---|
| Authenticated user | Cookie middleware | `[RequireAuth]` attribute + `AuthPolicyMiddleware` |
| Guild membership | Blizzard roster lookup | Same |
| Guild admin (rank 0) | Checked per request | `GuildPermissions.IsAdminAsync()` |
| Rank permissions | `rankPermissions[]` in guild doc | Same |
| Site admin | Key Vault secret lookup | `SiteAdminService` with config list |
| Run visibility (PUBLIC/GUILD) | Query filter | Same query filter |

### Test Mode / E2E

TypeScript used `TEST_MODE=true` env var with `testAuthScenario` query params to mock Battle.net responses. .NET has a dedicated `GET /api/e2e/login` endpoint. Both achieve the same goal for E2E testing without real Battle.net.

---

## 4. Frontend

### Pages/Routes

| Route | TypeScript (React) | .NET (Blazor) | Status |
|---|---|---|---|
| `/` | LandingPage | LandingPage | Ported |
| `/login` | LoginPage | LoginPage | Ported |
| `/login/success` or `/auth/success` | LoginSuccessPage | LoginSuccessPage | Ported (route changed) |
| `/login/failed` or `/auth/failure` | LoginFailedPage | LoginFailedPage | Ported (route changed) |
| `/goodbye` | GoodbyePage | GoodbyePage | Ported |
| `/privacy` | PrivacyPolicyPage | PrivacyPolicyPage | Ported |
| `/characters` or `/me` | CharactersPage | CharactersPage | Ported |
| `/guild` | GuildPage | GuildPage | Ported |
| `/guild/admin` | GuildAdminPage | GuildAdminPage | Ported |
| `/raids` or `/runs` | RaidsPage | RunsPage | Ported (renamed) |
| `/raids/new` or `/runs/new` | CreateRaidPage | CreateRunPage | Ported (renamed) |
| `/raids/:id/edit` or `/runs/:id/edit` | EditRaidPage | EditRunPage | Ported (renamed) |
| `/instances` | Not present | InstancesPage | New page |

### UI Components — Ported

| Component | TypeScript | .NET | Notes |
|---|---|---|---|
| Loading/error/empty states | `LoadingState`, `ErrorState`, `EmptyState` | `LoadingState<T>` discriminated union + `FluentMessageBar` | Equivalent |
| Character portraits | Character cards with portrait images | Character cards with portrait + initials fallback | Equivalent |
| Guild crest | `<img>` from crest URL | `<img>` from `CrestEmblemUrl` | Equivalent |
| Confirm dialogs | Reusable `ConfirmDialog` component | Inline confirmation cards | Functionally equivalent, less reusable |
| Raid/run roster | `RaidRosterGrid` with grouped sections | `FluentDataGrid` flat table | See gap below |
| Character selection | Cards with select action | Cards with select action | Equivalent |
| Account deletion | "Forget Me" section | Requires typing "FORGET ME" | Enhanced confirmation |
| NavBar | MUI AppBar + responsive hamburger | `FluentStack` horizontal nav | See gap below |
| Auth guards | `<AuthGuard>` React component | `[Authorize]` attribute + `RedirectToLogin` | Equivalent |
| Run form (create/edit) | React Hook Form + Zod validation | Blazor EditForm | Equivalent |
| Guild settings editor | Permission matrix table | Permission matrix with dropdowns | Equivalent |
| Privacy policy | Localized page + obfuscated email | Static page + honeypot email | Equivalent |

### UI Components — Missing or Degraded

| Feature | TypeScript | .NET | Impact |
|---|---|---|---|
| **Dark/light mode** | MUI theme with `prefers-color-scheme` + CSS variables | No theme switching; Fluent UI tokens only | **Medium** — users cannot toggle theme |
| **Toast/snackbar notifications** | `ToastContext` with success/error snackbars | `FluentMessageBar` inline messages only | **Low** — messages still shown, just inline rather than overlay |
| **WoW class colors** | `classColors` mapping for color-coded roster display | Plain text class names | **Medium** — roster is less visually distinctive |
| **Roster sections by attendance** | Grouped by IN/OUT/BENCH/LATE/AWAY with class grouping | Flat data grid | **Medium** — harder to scan roster at a glance |
| **Footer** | Footer component with links | No footer | **Low** |
| **Mobile hamburger menu** | MUI responsive drawer | No responsive collapse | **Medium** — nav may overflow on mobile |

---

## 5. Internationalization (i18n)

| Aspect | TypeScript | .NET | Status |
|---|---|---|---|
| Translation library | i18next + react-i18next | None | **Missing** |
| Locale files | `en.json`, `fi.json` | None | **Missing** |
| User locale persistence | Cosmos field + API | Cosmos field + API endpoint exists | **Backend ready** |
| Language switcher UI | Footer component | None | **Missing** |
| Translated UI strings | All strings via `t()` keys | All strings hardcoded in English | **Missing** |
| Guild-level locale | Not present | Supported (8 locales for date pickers) | **New feature** |
| Frontend `MeClient.UpdateAsync` | Used to persist locale | Method not implemented | **Missing** |

**Summary:** Backend infrastructure for locale persistence is ~70% complete. Frontend has zero localization — all strings are hardcoded English. A design spec exists at `docs/superpowers/specs/2026-03-30-i18n-infrastructure-design.md` but has not been implemented.

---

## 6. Cross-Cutting Concerns

### Fully Ported

| Concern | TypeScript | .NET |
|---|---|---|
| CORS middleware | Custom middleware | `CorsMiddleware` |
| Auth middleware | Cookie extraction + decryption | `AuthMiddleware` + `AuthPolicyMiddleware` |
| Audit logging | Application Insights | `AuditMiddleware` + ILogger |
| Request validation | Zod schemas | FluentValidation + data annotations |
| Error responses | `{ error: string }` | `{ error: string }` or `{ errors: string[] }` |
| Raider cleanup timer | Daily 04:00 UTC, 90 days | Daily 04:00 UTC, 90 days — identical |

### Missing

| Concern | TypeScript | .NET | Impact |
|---|---|---|---|
| **Rate limiting** | Per-IP: 10 auth/min, 30 writes/min | None | **High** — auth and write endpoints unprotected against brute force/abuse |
| **Security headers** | CSP, X-Content-Type-Options, X-Frame-Options, etc. | None | **High** — missing defense-in-depth headers |
| **Optimistic concurrency on signups** | ETag comparison + 3 retries on conflict | Single write, no ETag or retry | **Medium** — concurrent signups can overwrite each other |
| **Signup close time enforcement (backend)** | Not enforced (frontend only) | Not enforced (frontend only) | **Low** — same as TypeScript; both rely on frontend |

---

## 7. External API Integrations (Blizzard)

All Blizzard API integrations are fully ported:

| Integration | Status | Notes |
|---|---|---|
| OAuth authorize/token/userinfo | Ported | Same PKCE flow |
| Account profile summary | Ported | Same endpoint |
| Character profile summary | Ported | Same endpoint |
| Character media (portraits) | Ported | Same endpoint + caching |
| Character specializations | Ported | Same endpoint |
| Guild profile | Ported | Same endpoint |
| Guild roster | Ported | Same endpoint |
| Guild crest media | Ported | Emblem + border stored separately |
| Journal instances (reference) | Ported | Stored in Cosmos vs blob |
| Playable specializations (reference) | Ported | Stored in Cosmos vs blob |
| Playable classes/races | Not ported | TypeScript fetched these for reference; .NET uses class data from character profiles only |

**Resilience:** TypeScript used axios with default timeouts. .NET uses `Microsoft.Extensions.Http.Resilience` with retry + circuit breaker policies (20s/30s timeouts). This is an improvement.

---

## 8. Build & Deployment

| Aspect | TypeScript | .NET |
|---|---|---|
| Frontend build | Vite → SPA bundle | `dotnet publish` → Blazor WASM |
| Backend build | TypeScript → Node.js Functions | `dotnet build` → .NET Isolated Functions |
| Frontend deploy target | Azure Static Web Apps | Azure Static Web Apps |
| Backend deploy target | Azure Functions (Node.js 20) | Azure Functions (Flex Consumption FC1) |
| Infrastructure | Bicep modules | Bicep modules (expanded) |
| CI/CD | GitHub Actions | GitHub Actions |
| Linting | ESLint | `dotnet format` |
| Bundle size gate | Custom script | `scripts/check-bundle-size.sh` |
| Migration system | umzug (runtime migrations) | Bicep (declarative) + manual backfill scripts |

---

## 9. Gap Priority Matrix

### Must Fix (Security/Data Integrity)

| # | Gap | Risk | Effort |
|---|---|---|---|
| 1 | Rate limiting on auth endpoints | Brute force / credential stuffing | Medium |
| 2 | Rate limiting on write endpoints | Abuse / spam | Medium |
| 3 | Security response headers (CSP, HSTS, X-Frame-Options) | XSS, clickjacking | Low |
| 4 | Optimistic concurrency on run signups | Lost updates under concurrent signups | Medium |

### Should Fix (Feature Parity)

| # | Gap | Impact | Effort |
|---|---|---|---|
| 5 | WoW class colors in roster | Visual quality regression | Low |
| 6 | Roster grouped by attendance status | Usability regression | Medium |
| 7 | Mobile responsive nav (hamburger menu) | Mobile usability | Medium |
| 8 | Dark/light mode toggle | User preference | Medium |

### Nice to Have

| # | Gap | Impact | Effort |
|---|---|---|---|
| 9 | Toast/snackbar notifications | UI polish | Low |
| 10 | Footer component | UI completeness | Low |
| 11 | Frontend i18n (en + fi) | Finnish-speaking users | High |
| 12 | Guild admin by slug endpoint | Admin tooling | Low |
| 13 | `MeClient.UpdateAsync` for locale persistence | Blocked by missing i18n | Low |

---

## 10. Conclusion

The .NET port successfully reproduces all primary user workflows: logging in via Battle.net, managing characters, creating and signing up for runs, and managing guild settings. The Blizzard API integration, Cosmos DB data layer, and authentication system are feature-complete.

The port also introduces improvements not present in the TypeScript version: a readiness health probe, HTTP resilience policies (retry + circuit breaker), explicit raider TTL for data hygiene, dedicated reference data containers, and ASP.NET Core Data Protection for session encryption.

The most significant gaps are in security hardening (rate limiting, security headers) and UI polish (class colors, roster grouping, theming). These should be prioritized before considering the port production-equivalent to the TypeScript version from a security posture perspective.
