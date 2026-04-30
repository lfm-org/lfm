// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Newtonsoft.Json;
using Lfm.Api.Serialization;

namespace Lfm.Api.Repositories;

/// <summary>
/// Run document as stored in the Cosmos "runs" container.
/// Partition key: /id
/// Only the fields needed for raider scrubbing are modelled here.
/// Additional fields will be added as later endpoints (runs-related) are ported.
/// </summary>
public sealed record RunCharacterEntry(
    string Id,
    string CharacterId,
    string CharacterName,
    string CharacterRealm,
    int CharacterLevel,
    int CharacterClassId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string CharacterClassName,
    int CharacterRaceId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string CharacterRaceName,
    string RaiderBattleNetId,
    string DesiredAttendance,
    string ReviewedAttendance,
    int? SpecId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? SpecName,
    string? Role);

/// <summary>
/// Run document in the Cosmos "runs" container. Partition key: /id.
///
/// <para>
/// <b>Mode schema.</b> <see cref="Difficulty"/> + <see cref="Size"/> + <see cref="KeystoneLevel"/>
/// are the canonical typed mode fields; <see cref="ModeKey"/> is the legacy
/// composite (<c>"{Difficulty}:{Size}"</c>) retained for one cycle for
/// cross-compatibility while the migration populates the new fields on
/// existing Cosmos documents. New writes populate both; consumers prefer
/// the typed fields and fall back to parsing <see cref="ModeKey"/> when
/// the new fields are empty.
/// </para>
/// <para>
/// <b>Instance id/name.</b> Both are nullable: a Mythic+ "any dungeon"
/// session has no specific instance. All other run kinds require an
/// instance, which validators enforce on the request DTOs.
/// </para>
/// </summary>
public sealed record RunDocument(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string ModeKey,
    string Visibility,
    string CreatorGuild,
    int? CreatorGuildId,
    int? InstanceId,
    [property: JsonConverter(typeof(LocalizedStringConverter))] string? InstanceName,
    string? CreatorBattleNetId,
    string CreatedAt,
    int Ttl,
    IReadOnlyList<RunCharacterEntry> RunCharacters,
    string Difficulty = "",
    int Size = 0,
    int? KeystoneLevel = null,
    // Server-only set of battleNetIds the run owner has rejected. When a
    // raider in this list signs up (or re-signs up after a cancel), the
    // signup handler defaults their ReviewedAttendance to "OUT" instead of
    // "IN" to defend against the cancel-then-resignup bypass.
    // Empty by default; never projected to wire DTOs.
    IReadOnlyList<string>? RejectedRaiderBattleNetIds = null,
    [property: System.Text.Json.Serialization.JsonPropertyName("_etag")] string? ETag = null);

/// <summary>
/// One page of runs returned by the list endpoints. <see cref="ContinuationToken"/>
/// is the opaque Cosmos continuation token to pass back for the next page; a
/// null value means the server has no more results.
/// </summary>
public sealed record RunsPage(IReadOnlyList<RunDocument> Items, string? ContinuationToken);

public interface IRunsRepository
{
    /// <summary>
    /// Scrubs all references to the given battleNetId from run documents in the
    /// "runs" container. For each matching run:
    ///   - removes entries from runCharacters where raiderBattleNetId == battleNetId
    ///   - nulls creatorBattleNetId when it equals battleNetId
    /// Only runs that are actually modified are written back. Mirrors the TS
    /// scrubRaiderFromRuns implementation in functions/src/lib/raider-cleanup.ts.
    /// </summary>
    Task ScrubRaiderAsync(string battleNetId, CancellationToken ct);

    /// <summary>
    /// Returns one page of runs visible to a user who belongs to the given guild.
    /// Visibility rules (mirrors runs-list.ts):
    ///   - PUBLIC runs (visible to everyone)
    ///   - GUILD runs created by the same guild (creatorGuildId matches)
    ///   - runs created by the user themselves (creatorBattleNetId matches)
    /// Ordered by startTime ascending. Caps the page at <paramref name="top"/> items
    /// via Cosmos <c>MaxItemCount</c>; returns a continuation token when more
    /// pages are available.
    /// </summary>
    Task<RunsPage> ListForGuildAsync(string guildId, string battleNetId, int top, string? continuationToken, CancellationToken ct);

    /// <summary>
    /// Returns one page of runs visible to a user who has no guild.
    /// Visibility rules:
    ///   - PUBLIC runs
    ///   - runs created by the user themselves
    /// Ordered by startTime ascending. Paginated the same way as
    /// <see cref="ListForGuildAsync"/>.
    /// </summary>
    Task<RunsPage> ListForUserAsync(string battleNetId, int top, string? continuationToken, CancellationToken ct);

    /// <summary>
    /// Returns a single run by its document id, or null if it does not exist.
    /// Uses a point read (partition key == id) for minimal RU cost.
    /// Mirrors container.item(id, id).read() in runs-detail.ts.
    /// </summary>
    Task<RunDocument?> GetByIdAsync(string id, CancellationToken ct);

    /// <summary>
    /// Creates a new run document in the "runs" container.
    /// Returns the persisted document (including any server-side fields set by Cosmos).
    /// Mirrors <c>getRunsContainer().items.create(run)</c> in runs-create.ts.
    /// </summary>
    Task<RunDocument> CreateAsync(RunDocument run, CancellationToken ct);

    /// <summary>
    /// Replaces an existing run document in the "runs" container.
    ///
    /// <para>
    /// <paramref name="ifMatchEtag"/> is the optimistic-concurrency token. When
    /// non-null it is passed as <c>IfMatchEtag</c> to Cosmos; when null the
    /// repository falls back to <c>run.ETag</c>. Callers surfacing a client
    /// <c>If-Match</c> header should pass it explicitly so that a stale token
    /// produces a 412-flavoured <see cref="ConcurrencyConflictException"/> even
    /// when the <paramref name="run"/> document was re-fetched between read
    /// and write.
    /// </para>
    /// </summary>
    Task<RunDocument> UpdateAsync(RunDocument run, string? ifMatchEtag, CancellationToken ct);

    /// <summary>
    /// Deletes a run document by its id. Idempotent: if the document does not
    /// exist (Cosmos 404), the call succeeds silently.
    /// Mirrors <c>getRunsContainer().item(id, id).delete()</c> in runs-delete.ts.
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct);

    /// <summary>
    /// Backfills <c>Difficulty</c> / <c>Size</c> / <c>KeystoneLevel</c> on every
    /// <see cref="RunDocument"/> that predates the PR 5 schema. Idempotent —
    /// documents whose typed fields are already populated are left untouched.
    /// When <paramref name="dryRun"/> is true, returns the would-migrate count
    /// without writing anything. Admin-only; callers must gate on
    /// <see cref="Services.ISiteAdminService"/>.
    /// </summary>
    Task<RunMigrationResult> MigrateSchemaAsync(bool dryRun, CancellationToken ct);
}

public sealed record RunMigrationResult(int Scanned, int Migrated, bool DryRun);
