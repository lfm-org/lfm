namespace Lfm.Api.Auth;

public sealed record SessionPrincipal(
    string BattleNetId,
    string BattleTag,
    string? GuildId,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
