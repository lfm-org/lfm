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
    string CharacterClassName,
    int CharacterRaceId,
    string CharacterRaceName,
    string RaiderBattleNetId,
    string DesiredAttendance,
    string ReviewedAttendance,
    int? SpecId,
    string? SpecName,
    string? Role);

public sealed record RunDocument(
    string Id,
    string StartTime,
    string SignupCloseTime,
    string Description,
    string ModeKey,
    string Visibility,
    string CreatorGuild,
    int? CreatorGuildId,
    int InstanceId,
    string InstanceName,
    string? CreatorBattleNetId,
    string CreatedAt,
    int Ttl,
    IReadOnlyList<RunCharacterEntry> RunCharacters);

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
    /// Returns all runs visible to a user who belongs to the given guild.
    /// Visibility rules (mirrors runs-list.ts):
    ///   - PUBLIC runs (visible to everyone)
    ///   - GUILD runs created by the same guild (creatorGuildId matches)
    ///   - runs created by the user themselves (creatorBattleNetId matches)
    /// Ordered by startTime ascending.
    /// </summary>
    Task<IReadOnlyList<RunDocument>> ListForGuildAsync(string guildId, string battleNetId, CancellationToken ct);

    /// <summary>
    /// Returns all runs visible to a user who has no guild.
    /// Visibility rules:
    ///   - PUBLIC runs
    ///   - runs created by the user themselves
    /// Ordered by startTime ascending.
    /// </summary>
    Task<IReadOnlyList<RunDocument>> ListForUserAsync(string battleNetId, CancellationToken ct);
}
