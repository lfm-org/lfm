namespace Lfm.Contracts.Me;

/// <summary>
/// Response shape for GET /api/me.
/// Fields mirror the TypeScript handler at functions/src/functions/me.ts.
/// </summary>
public sealed record MeResponse(
    string BattleNetId,
    string? GuildName,
    string? SelectedCharacterId,
    bool IsSiteAdmin,
    string? Locale);
