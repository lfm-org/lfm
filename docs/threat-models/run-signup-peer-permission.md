# Threat Model: Run Signup and Peer Permission

## Introduction

This document covers the trust boundary between two authenticated users where one
mutates the other's run document via the signup roster. Run documents are
partitioned by run id, not by user, so partition isolation does **not** isolate
users on this resource. Authorization is policy-only, evaluated at the handler
layer through `IGuildPermissions` (rank checks against the cached guild roster
inside the run's owning guild).

`POST /api/runs/{id}/signup` and `DELETE /api/runs/{id}/signup` are the two writes
across this boundary. The combination is the source of the canonical
**resignup-bypass** weakness: cancelling a signup and re-creating it discards any
prior `ReviewedAttendance` decision the run owner had set.

## Data Flow

```
Auth'd Browser (Caller A)            Functions API                      Cosmos DB (runs)
       |                                   |                                    |
       |--POST /api/runs/{id}/signup------>|                                    |
       |    body: { CharacterId, ... }     |                                    |
       |                       [boundary: caller A -> roster on run owned by B]
       |                                   |--Auth: principal=A                  |
       |                                   |--Read raider(A)-------->Cosmos(raiders)
       |                                   |--Read run(id)----------------------->|
       |                                   |<--RunDocument {RunCharacters[], ...}-|
       |                                   |--Visibility check                   |
       |                                   |    GUILD -> guildPermissions        |
       |                                   |        .CanSignupGuildRunsAsync(A)  |
       |                                   |--existingIndex = find A in roster   |
       |                                   |--reviewedAttendance =               |
       |                                   |    existing >=0 ? entry.Reviewed    |
       |                                   |                 : "IN"              |
       |                                   |--Replace run(if-match etag)-------->|
       |<--200 + sanitised RunCharacters---|                                    |
       |                                                                        |
       |--DELETE /api/runs/{id}/signup---->|                                    |
       |                                   |--remove A's entry from roster       |
       |                                   |--Replace run(if-match etag)-------->|
       |                                                                        |
       | A re-POSTs with same character id                                      |
       |--POST /api/runs/{id}/signup------>|                                    |
       |                                   |--existingIndex = -1 (just removed)  |
       |                                   |--reviewedAttendance = "IN" (reset)  |
```

## Trust Boundaries Crossed

- **Caller A → run owned by B**: A signs up to a run B created. The mutation
  attaches a `RunCharacterEntry` with `RaiderBattleNetId = principal.BattleNetId`
  to B's run document.
- **Cached guild roster → permission decision**: `IGuildPermissions` reads
  `GuildDocument.Roster` (populated by a separate enrichment path) and matches the
  caller's character to a rank entry. Roster freshness is its own concern — a
  former member who has not been removed from the cached roster still satisfies
  `CanSignupGuildRunsAsync`.
- **Optimistic-concurrency loop → user-perceived semantics**: signup retries up to
  3 times on `ConcurrencyConflictException`. Two simultaneous signups for the same
  user race; the loser retries.

## STRIDE Table

| Category | Threat | Current mitigation | Residual risk |
|---|---|---|---|
| **Spoofing** | Caller A signs up using caller B's character id (`body.CharacterId` references a character that lives on B's `RaiderDocument`). | The handler loads the caller's own `RaiderDocument` by `principal.BattleNetId` and looks up `body.CharacterId` *within that raider's character list*; if the id is not on the caller's raider, the handler returns 404 `signup-not-found-character`. A's session cannot reach B's character ids. | Low — character ownership is enforced at handler layer. |
| **Tampering — resignup attendance reset** | Run owner B sets A's `ReviewedAttendance` to `"OUT"` (rejected). A cancels their signup (`DELETE /signup`) then re-signs up (`POST /signup`); on the re-signup, `existingIndex < 0` so `reviewedAttendance` defaults to `"IN"`, silently reversing B's rejection. | None server-side. The flow is a legitimate user editing their signup; the cancel + resign path is allowed for self-edit. There is no per-`(runId, raiderBattleNetId)` history of prior reviewed-attendance decisions. | **High — exploitable today.** Fix: the resignup path must preserve any existing-or-prior `OUT` decision. Options: (a) keep the entry on cancel but mark `Cancelled = true`, so `existingIndex >= 0` on resignup; (b) maintain a per-run rejection list separate from the roster; (c) require run-owner re-approval after cancel. Tracked as a code fix, not a doc-only finding. |
| **Tampering — concurrent roster writes** | Two simultaneous signup attempts for the same caller produce a 412 from Cosmos; the lose-side retries. | `RunsRepository.UpdateAsync` enforces `IfMatchEtag` and throws `ConcurrencyConflictException`; handler retries up to 3 times before returning 409 problem+json. | Low — bounded retries, no infinite loop. Acceptable user experience under contention. |
| **Repudiation** | A run owner disputes whether a signup or cancel happened. | `AuditLog.Emit("signup.create" / "signup.cancel", principal.BattleNetId, runId, ...)` records every mutation through this boundary. Cosmos `_etag` history is implicit (not preserved between writes). | Medium — App Insights ingestion is best-effort. No per-document audit of the *content* of each roster mutation (e.g. which entry was added, removed, or re-`IN`-ified). Forensics on the resignup-bypass requires correlating the cancel + create pair. |
| **Information disclosure — guild run roster leakage** | All authenticated callers who can list the run (`GET /runs` or `GET /runs/{id}`) see the full sanitised roster: every signup's character name, class, spec, role, `DesiredAttendance`, `ReviewedAttendance`. For GUILD-scoped runs, "all callers" means "all guild members." | `RaiderBattleNetId` is stripped from the wire DTO via `Sanitize`. Other identifying fields stay. | Low-Medium — by design (a sign-up sheet that members cannot see is not useful). The `ReviewedAttendance` value reveals who the run owner has accepted vs rejected, which is sensitive social information; documented as an accepted trade-off. |
| **Denial of service** | Caller floods POST/DELETE on a single run id; each operation reads + replaces the entire run document. | `RateLimitMiddleware` applies the standard origin rate limit. ETag-based optimistic concurrency means concurrent floods just lose retries. | Medium — consumes RU on the runs container; in the free-tier 1000 RU/s budget, sustained flooding from one user could starve others. Currently no per-user RU cap. |
| **Elevation of privilege — guild rank bypass** | An ex-guild member whose Blizzard guild membership has lapsed but whose `GuildDocument.Roster` cache has not been refreshed still passes `CanSignupGuildRunsAsync` and signs up. | `GuildPermissions` documents the dependency on roster freshness; the roster is refreshed by a separate enrichment flow. The permission helper already returns `false` when roster is absent (`GuildPermissions.cs:59` comment). | Medium — depends on roster-refresh cadence (currently driven by user-triggered enrichment). A staleness window is unavoidable without active push from Blizzard. Documented constraint. |

## Key Code References

- `api/Functions/RunsSignupFunction.cs:29` — handler doc-comment outlining the
  permission flow.
- `api/Functions/RunsSignupFunction.cs:144-156` — visibility check + GUILD rank
  gate via `IGuildPermissions.CanSignupGuildRunsAsync`.
- `api/Functions/RunsSignupFunction.cs:166-175` — roster lookup by
  `RaiderBattleNetId == principal.BattleNetId`.
- `api/Functions/RunsSignupFunction.cs:177-179` — entryId reused if existing; new
  GUID otherwise.
- `api/Functions/RunsSignupFunction.cs:181-183` — **the resignup-bypass:**
  `reviewedAttendance = existingIndex >= 0 ? existing.ReviewedAttendance : "IN"`.
- `api/Functions/RunsSignupFunction.cs:185-201` — `RunCharacterEntry` construction.
- `api/Functions/RunsCancelSignupFunction.cs:21-27` — handler doc; cancel removes
  the entry rather than marking it cancelled.
- `api/Functions/RunsCancelSignupFunction.cs:55-61` — visibility check for GUILD
  runs.
- `api/Functions/RunsCancelSignupFunction.cs:97-102` — repository update + audit
  emit.
- `api/Services/GuildPermissions.cs:102-144` — `CanSignupGuildRunsAsync` —
  matches caller's character to a rank entry in the cached roster.
- `api/Services/GuildPermissions.cs:59-66, 109-116, 158-165` — staleness comment:
  returns `false` when roster is absent or stale.
- `api/Repositories/RunsRepository.cs:112-133` — `UpdateAsync(run, ifMatchEtag)`
  throws `ConcurrencyConflictException` on 412; consumed by the 3-attempt retry
  loop in the signup handler.
- `shared/Lfm.Contracts/RunCharacterEntry.cs` — DTO; `ReviewedAttendance` field is
  the persistence shape carrying the bypass.

## Out of Scope

- The Cosmos partition-key boundary on the `runs` container — covered by
  `cosmos-partition-key-authz.md`.
- The session cookie that supplies the principal — covered by
  `session-cookie-api.md`.
- Run creation and deletion authorization (run-creator ownership) — covered
  partially by `cosmos-partition-key-authz.md`'s Elevation row; this model
  intentionally focuses on the roster mutation flows.
- Guild roster freshness / Blizzard-side membership state — driven by a separate
  enrichment path (`BattleNetCharactersRefreshFunction`); covered indirectly by
  `blizzard-outbound-api.md`.
- Privacy / consent for displaying `ReviewedAttendance` to other guild members —
  product-level decision, accepted trade-off documented above.
