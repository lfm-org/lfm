namespace Lfm.Api.Services;

/// <summary>
/// Blizzard OAuth client. Split into what is needed task-by-task:
///   B2.1 (login)    — <see cref="GenerateState"/> + <see cref="BuildAuthorizeUrl"/>
///   B2.2 (callback) — <see cref="ExchangeCodeAsync"/> + <see cref="GetUserInfoAsync"/> (stubs for now)
///   B2.3 (logout)   — no additional methods needed
/// Design choice: single interface rather than splitting into multiple
/// partial-capability interfaces, because all three tasks share the same
/// underlying configuration (BlizzardOptions) and the interface stays small.
/// Stub methods throw NotImplementedException so they compile but fail loudly
/// if called before B2.2 replaces them.
/// </summary>
public interface IBlizzardOAuthClient
{
    /// <summary>
    /// Generates a cryptographically random, URL-safe state token for use as
    /// the OAuth <c>state</c> parameter (CSRF protection).
    /// B2.1 uses a random GUID ("N" format, 32 hex chars) — simple and non-empty.
    /// B2.2 will upgrade this to a PKCE+redirect payload sealed via IDataProtector
    /// so it can be validated statlessly on callback.
    /// </summary>
    string GenerateState();

    /// <summary>
    /// Builds the Battle.net OAuth authorization URL including all required
    /// query parameters: client_id, redirect_uri, response_type=code,
    /// scope=wow.profile, state.
    /// </summary>
    /// <param name="state">A non-empty CSRF state token (see <see cref="GenerateState"/>).</param>
    /// <returns>The fully-formed authorization URL string.</returns>
    string BuildAuthorizeUrl(string state);

    // ---------------------------------------------------------------------------
    // B2.2 stubs — implemented in the callback task.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Exchanges an authorization code for an access token.
    /// Implemented in B2.2.
    /// </summary>
    Task<BlizzardTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the authenticated user's profile from the Battle.net userinfo endpoint.
    /// Implemented in B2.2.
    /// </summary>
    Task<BlizzardUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
}

/// <summary>Token response from the Battle.net OAuth token endpoint.</summary>
/// <param name="AccessToken">The Bearer access token.</param>
/// <param name="ExpiresIn">Lifetime in seconds.</param>
public sealed record BlizzardTokenResponse(string AccessToken, int ExpiresIn);

/// <summary>User identity returned by the Battle.net userinfo endpoint.</summary>
/// <param name="Id">Battle.net account identifier (numeric, opaque).</param>
/// <param name="BattleTag">Human-readable BattleTag, e.g. "Player#1234".</param>
public sealed record BlizzardUserInfo(long Id, string BattleTag);
