# Storage Architecture

This document is the rule for where any piece of data the app persists should live. When adding a new data kind — a new Blizzard response you fetch, a new per-user setting, a new cache — start here. When reviewing a change that touches storage, measure it against the decision flow below.

## The rule

| Kind | Store | Location |
|---|---|---|
| **Static Blizzard reference data** (journal-instance, playable-specialization, playable-class, playable-race, hero-talent-tree — same shape for every user) | **Blob** | `lfmstore/wow/reference/{kind}/{id}.json` + `reference/{kind}/index.json` manifest |
| **Dynamic per-user / per-guild / per-run data** (raider profiles, run signups, guild docs, admin overrides) | **Cosmos** | `lfm-cosmos/lfm/{raiders,runs,guilds}` |
| **Per-entity caches of Blizzard responses** (one user's account profile, one guild's roster) | **Cosmos, embedded** | inside the owning raider/guild document, with a `*FetchedAt` timestamp |
| **Operational bytes** (ASP.NET Data Protection keys, Functions runtime state, deploy zip artifacts) | **Blob** | `lfmstore/{dataprotection,deployments,azure-webjobs-hosts,azure-webjobs-secrets}` |
| **Secrets** (OAuth client secret, DP wrapping key) | **Key Vault** | referenced from App Settings via Key Vault references |
| **Blizzard media image bytes** | **Blob** | `lfmstore/wow/media-cache/render/{sha256}.{ext}`, served through `/api/v1/wow/media/cache` |

## Why the split

- **Cost.** The Cosmos account is on free tier: 1000 RU/s + 25 GB, shared across every container in the account. Every full-scan `SELECT FROM c` on reference data competes with point-reads on `runs` and `raiders`. Reference data changes on patch days; reading it from Cosmos would consume RU forever for data that doesn't vary per user. Blob reads of ~5 MB of JSON across ~250 files run at effectively $0 for this project's scale.
- **Cold-boot independence.** The blob store is ingested from Blizzard on a weekly timer (`WowReferenceRefreshTimerFunction`) and on admin demand (`POST /api/wow/reference/refresh`). If Blizzard's Game Data API is down, `/api/wow/reference/instances` and `/api/wow/reference/specializations` still serve the last-known good data from blob. Cosmos-sourced reference data would need to be re-populated from Blizzard at deploy time to exist at all.
- **Mental model.** `raiders`, `runs`, `guilds` are "things that change because a user did something". Everything in `wow/reference/` changes only because Blizzard shipped a patch. Keeping those two lifecycles in separate stores makes backup, retention, and reasoning obvious.
- **Converter placement.** Blizzard's no-locale responses store names as localized objects (`{"en_US": "…", "de_DE": "…", …}`). `Lfm.Api.Serialization.LocalizedStringConverter` handles that shape. It belongs on the *Blizzard-shape* records (`BlizzardJournalInstanceDetail.Name`, etc.) that we deserialize from blob, not on our own DTOs. Keeping reference data in blob keeps the converter on the exact types that need it.

## Image caching policy

Blizzard media documents may contain `https://render.worldofwarcraft.com/...`
asset URLs. Store those source URLs next to the owning reference or user cache,
but do not render them directly in browser image tags.

Browser-facing image URLs must go through `/api/v1/wow/media/cache?source=...`:

- The `source` value is base64url-encoded.
- The API accepts only HTTPS URLs on `render.worldofwarcraft.com`.
- The API first reads `wow/media-cache/render/{sha256}.{ext}` from blob.
- On a cache miss, the API fetches the Blizzard image, validates an `image/*`
  content type and the size cap, stores the bytes in blob, then serves our copy.
- The SWA CSP allows `img-src 'self' https://{{API_HOSTNAME}}`; it does not
  allow direct `render.worldofwarcraft.com` image loads.

The source URL remains a string in the record that discovered it:

- Spec icons: `reference/playable-specialization-media/{id}.json` → surfaced as `SpecializationDto.IconUrl` after the API/app maps the source to the media-cache URL.
- Instance portraits: `reference/journal-instance-media/{id}.json` → surfaced as `InstanceDto.PortraitUrl` after the API/app maps the source to the media-cache URL.
- Character portraits: `raiders.PortraitCache` (Cosmos map of `{region-realm-name → render URL}` inside the owner's doc — not shared, not reference data, stays per-user) → surfaced as a media-cache URL.
- Guild crest fields, if populated, use the same media-cache URL mapping before rendering.

## Decision flow for a new data kind

```
1. Is it a function of one user / one guild / one run?
   yes → Cosmos, in the owning doc (raiders / runs / guilds)
   no  → continue

2. Is it a verbatim Blizzard reference response, same shape for every user?
   yes → Blob, under wow/reference/{kind}/
   no  → continue

3. Is it binary we produce or consume (zip, image we've chosen to cache, pre-built report)?
   yes → Blob, in a purpose-named container
   no  → continue

4. Is it a short-lived cache of a Blizzard call for one specific user?
   yes → embed inside that user's Cosmos doc with a *FetchedAt timestamp.
         Don't introduce a separate cache store.
   no  → continue

5. Still unclear?
   default: blob for reference-shaped data, Cosmos for anything with
   per-entity lifecycle. Write the trade-off down in this file.
```

## Index manifest convention

For every reference kind that has a list endpoint, the ingester emits `reference/{kind}/index.json` carrying every field the list endpoint needs. The read path is one blob GET per list call — not one listing call plus N detail reads. Shape (conceptual):

```json
// reference/playable-specialization/index.json
[
  { "id": 62, "name": "Arcane", "classId": 8, "role": "RANGED_DPS",
    "iconUrl": "https://render.worldofwarcraft.com/.../arcane.jpg" },
  ...
]

// reference/journal-instance/index.json
[
  { "id": 1200, "name": "Liberation of Undermine",
    "modes": [ { "modeKey": "NORMAL:25" }, { "modeKey": "HEROIC:25" } ],
    "expansion": "The War Within",
    "portraitUrl": "https://render.worldofwarcraft.com/.../tile.jpg" },
  ...
]
```

The per-id detail blobs (`{kind}/{id}.json`, `{kind}-media/{id}.json`) stay for future endpoints (detail pages, hero talents) and as the source of truth the manifest is derived from. Manifest media fields store Blizzard source URLs; HTTP handlers and Blazor image components convert them to `/api/v1/wow/media/cache` before browser rendering.

## Known exceptions to flag

These look like they violate the rule but don't, because they are per-entity *caches* of Blizzard responses, not shared reference data. They stay embedded in the owning Cosmos document:

- `raiders.PortraitCache` — map of `characterId → render URL`, refreshed by the portrait-fetch flow, tied to the raider doc's TTL.
- `raiders.AccountProfileSummary` — cached Blizzard WoW account profile, tracked by `AccountProfileFetchedAt`.
- `raiders.Characters[*].MediaSummary` — cached Blizzard character media, tracked by `MediaFetchedAt`.
- `guilds.BlizzardRosterRaw` + `guilds.BlizzardProfileRaw` — cached Blizzard guild roster + profile, tracked by `BlizzardRosterFetchedAt` / `BlizzardProfileFetchedAt`.

Splitting any of these out to blob would add round-trips and a consistency surface for zero cost win, because each cache's lifecycle is *the owner's* lifecycle, not a shared patch-day lifecycle.

## Design tradeoffs

- **Cosmos Newtonsoft pin.** Cosmos SDK 3.x loads Newtonsoft.Json for round-trip even when `AzureCosmosDisableNewtonsoftJsonCheck=true`. Document records may carry Newtonsoft attributes for storage and STJ attributes for wire/test serialization. Revisit when the Cosmos SDK ships an STJ-default release.
- **`RunDocument.ModeKey` retirement criterion.** The legacy composite is kept for one schema-migration cycle. Remove `RunDocument.ModeKey`, the legacy fallback branch in `RunModeResolver.Resolve`, and `RunsMigrateSchemaFunction` once `MigrateSchemaAsync(dryRun: true)` reports zero un-migrated production documents.
- **Reference-data accessors.** New blob-backed reference data should call `IBlobReferenceClient` plus a static projection helper rather than spawning a new `I*Repository` interface, unless a real second implementation or test seam need exists.

## Production layout (2026-04-21)

```
Cosmos DB — lfm-cosmos / lfm
├── raiders         PK /battleNetId   per-user profile + embedded caches
├── runs            PK /id             per-run signups
└── guilds          PK /id             per-guild doc + embedded Blizzard caches

Blob — lfmstore
├── wow                                 static Blizzard reference data
│   └── reference/
│       ├── journal-instance/
│       ├── journal-instance-media/    (Phase 3+)
│       ├── playable-specialization/
│       ├── playable-specialization-media/
│       ├── playable-class/             (not yet consumed)
│       ├── playable-race/              (not yet consumed)
│       └── hero-talent-tree/           (future)
│   └── media-cache/
│       └── render/                      cached Blizzard render-CDN image bytes
├── dataprotection                      ASP.NET DP keys, KV-wrapped
├── deployments                         Functions zip deploy artifacts
├── azure-webjobs-hosts                 Functions runtime state (auto-managed)
└── azure-webjobs-secrets               Functions host secrets (auto-managed)

Key Vault — lfm-kv
├── OAuth client secret
└── DP wrapping key
```
