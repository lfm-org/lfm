namespace Lfm.Api.Repositories;

/// <summary>
/// Raider document as stored in the Cosmos "raiders" container.
/// Partition key: /battleNetId
/// Only the fields needed for the current set of ported endpoints are modelled here.
/// Additional fields will be added incrementally as B1.3 (me-delete),
/// and B5.1 (raider-character) are ported.
/// </summary>
public sealed record RaiderDocument(
    string Id,
    string BattleNetId,
    string? SelectedCharacterId,
    string? Locale,
    // Cosmos TTL in seconds. Set by me-update (180 * 86400 = ~180 days).
    // Null means no TTL override; the container default applies.
    int? Ttl = null);

public interface IRaidersRepository
{
    /// <summary>
    /// Point-read by battleNetId (which is both the document id and partition key).
    /// Returns null when the document does not exist.
    /// </summary>
    Task<RaiderDocument?> GetByBattleNetIdAsync(string battleNetId, CancellationToken ct);

    /// <summary>
    /// Upserts a raider document. Partition key is the document's BattleNetId.
    /// </summary>
    Task UpsertAsync(RaiderDocument raider, CancellationToken ct);

    /// <summary>
    /// Deletes the raider document identified by battleNetId (which is both the
    /// document id and partition key). Treats NotFound as success (idempotent).
    /// </summary>
    Task DeleteAsync(string battleNetId, CancellationToken ct);
}
