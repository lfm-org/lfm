# Wire Payload Contract

This document is the rule for what fields are allowed on the JSON the API sends to the Blazor app. When adding a property to a record under `shared/Lfm.Contracts/`, when reviewing a change that touches a wire DTO, or when auditing the API surface, measure the change against the rule below.

## The rule

Every property on a `shared/Lfm.Contracts/` record must have a live consumer in the Blazor app — either bound in Razor markup (`app/`) or read in code-behind / `app/Lfm.App.Core/` services. Test-only usage does not count: if the only place a field is referenced is `tests/`, the field doesn't earn its place on the wire.

The wire is the public contract between API and app. Storage shape (Cosmos documents, blob JSON) is separate — see [storage-architecture.md](storage-architecture.md). Server-internal state must stop at the API boundary.

## What must never appear on the wire

| Category | Examples | Why |
|---|---|---|
| Cosmos container internals | `Ttl`, partition keys, `_etag`, `_rid` | Storage concept, not a domain concept. Leaking it ossifies the wire to a specific persistence layer. |
| Audit timestamps | `CreatedAt`, `UpdatedAt`, `DeletedAt` | The app does not surface them. If the UI needs "X minutes ago", add the field then — until then, it's just bytes. |
| Other users' Battle.net ids | A run's `CreatorBattleNetId`, a roster member's `RaiderBattleNetId` | PII. Replace with a derived boolean (`IsCurrentUser`) or a non-identifying display string (`CreatorGuild`). The pattern in `RunCharacterDto.IsCurrentUser` is the reference. |
| Internal document ids the route doesn't expose | A nested-entry `Id` that no PUT/DELETE route accepts | If you can't act on the id from the client, sending it is just confusion. |
| Raw Blizzard pass-through payloads | The full `account-profile`, `journal-instance`, `playable-class` JSON | Always project to a Lfm-owned DTO. The mapping layer is the place to drop fields the app doesn't render. |

## Exceptions

Two — and only these two — narrow exceptions are allowed. Both must be marked in the code so the next audit recognises them.

### Peer permission fields

When a permission tuple is rendered together (e.g. `CanCreateGuildRuns` / `CanSignupGuildRuns` / `CanDeleteGuildRuns`), keep the unused peer for semantic completeness even when the UI only surfaces some members today. Rationale: shipping the permission set partial-by-partial creates a fragmented API surface where adding a UI hint requires a wire change.

Mark with an XML doc-comment naming the peer set, e.g.:

```csharp
/// <summary>
/// Kept for peer-permission symmetry with CanCreateGuildRuns/CanSignupGuildRuns,
/// even though no UI surfaces it today.
/// </summary>
public bool CanDeleteGuildRuns { get; }
```

### Planned near-term feature reservation

A field with no current consumer is allowed if a feature already in flight will read it within the next handful of changes. Annotate with an XML doc-comment naming the pending feature so the next audit knows why it's there. If the feature stalls or gets dropped, the field gets trimmed at the next audit — the reservation is not permanent.

Example today: `InstanceDto.PortraitUrl`. The portrait surface is in design; the field stays so the API ingester (`WowReferenceRefreshFunction`) doesn't need a re-shape when the UI lands.

## How to verify a new field

Before adding a property to a `shared/Lfm.Contracts/` record:

```bash
grep -rn "\.NewField" app/
```

At least one non-test hit must exist (or be in the same diff). If the only hits are under `tests/`, do not add the field — change the test or wait for the consumer.

## How to audit the contract

Run periodically (and after large refactors). The audit reads every property of every record under `shared/Lfm.Contracts/` and grep-checks `app/` for a non-test consumer. Fields with zero non-test consumers are candidates for trimming; verify each by hand against the two exceptions above before removing.

```bash
# List PascalCase property/parameter names declared in shared/Lfm.Contracts records.
# Cross-reference each against app/ (excluding tests/) for a real consumer.
git grep -hE '^\s*(public\s+\S+\s+|[A-Z]\w+\??\s)\s*[A-Z]\w+\s*[,;\)\{=]' shared/Lfm.Contracts/
```

The exact command is intentionally rough — the audit is human-driven, not automated. The point is to force a deliberate look at every wire field on a regular cadence, not to gate CI.

## Cross-references

- [storage-architecture.md](storage-architecture.md) — where the data lives at rest. The wire is *not* the storage shape; mapping in `api/Functions/` is what bridges them.
- `api/Functions/RunsListFunction.cs`, `api/Functions/GuildMapper.cs` — reference projection sites that show the storage-document → wire-DTO mapping pattern.
- `RunCharacterDto.IsCurrentUser` — the reference pattern for replacing an other-user identifier with a derived boolean.
