using Lfm.Api.Auth;

namespace Lfm.Api.Services;

/// <summary>
/// Resolves guild-level permissions for the authenticated principal.
/// Mirrors the TypeScript permission logic in
/// <c>functions/src/lib/guild/context.ts</c> (<c>resolveGuildEditor</c>).
/// </summary>
public interface IGuildPermissions
{
    /// <summary>
    /// Returns true if the principal is a guild admin (rank 0 in the Blizzard
    /// roster for their guild), mirroring the TypeScript <c>resolveGuildEditor</c>
    /// check: <c>canEdit = bestRank === 0</c>.
    /// </summary>
    Task<bool> IsAdminAsync(SessionPrincipal principal, CancellationToken ct);

    /// <summary>
    /// Returns true if the principal's matched guild rank has the
    /// <c>canCreateGuildRuns</c> permission, mirroring the TypeScript
    /// <c>getEffectiveGuildPermissions(...).canCreateGuildRuns</c> check in
    /// <c>functions/src/lib/guild-permissions.ts</c>.
    /// Returns false when the principal has no guild, the roster is not fresh,
    /// the raider is not in the roster, or the rank permission is not set.
    /// </summary>
    Task<bool> CanCreateGuildRunsAsync(SessionPrincipal principal, CancellationToken ct);
}
