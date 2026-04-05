namespace Lfm.Api.Auth;

public sealed record SessionPrincipal(
    string BattleNetId,
    string BattleTag,
    string? GuildId,
    string? GuildName,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
