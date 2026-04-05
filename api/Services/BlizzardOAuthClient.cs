using Lfm.Api.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Implements <see cref="IBlizzardOAuthClient"/> using Battle.net OAuth 2.0 endpoints.
///
/// State-handling approach (B2.1):
///   The TS implementation seals { state, codeVerifier, redirect } into a signed
///   HS256 JWT stored in a <c>login_state</c> HttpOnly cookie (5-min TTL). The
///   callback verifies the cookie to reconstruct the PKCE verifier and validate state.
///   For B2.1, we use a random GUID state (stateless, trivially non-empty). B2.2 will
///   upgrade <see cref="GenerateState"/> to produce a DataProtection-sealed payload
///   mirroring the TS JWT cookie approach, and the handler will set the cookie.
/// </summary>
public sealed class BlizzardOAuthClient(IOptions<BlizzardOptions> options) : IBlizzardOAuthClient
{
    private readonly BlizzardOptions _opts = options.Value;

    /// <inheritdoc/>
    public string GenerateState() => Guid.NewGuid().ToString("N");

    /// <inheritdoc/>
    public string BuildAuthorizeUrl(string state)
    {
        if (string.IsNullOrEmpty(state))
            throw new ArgumentException("State must not be empty.", nameof(state));

        var region = _opts.Region.ToLowerInvariant();
        var host = $"https://{region}.battle.net/oauth/authorize";

        var qb = new QueryBuilder
        {
            { "response_type", "code" },
            { "client_id",     _opts.ClientId },
            { "redirect_uri",  _opts.RedirectUri },
            { "scope",         "wow.profile" },
            { "state",         state }
        };

        return host + qb.ToQueryString();
    }

    // ---------------------------------------------------------------------------
    // B2.2 stubs
    // ---------------------------------------------------------------------------

    /// <inheritdoc/>
    public Task<BlizzardTokenResponse> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("ExchangeCodeAsync is implemented in B2.2.");

    /// <inheritdoc/>
    public Task<BlizzardUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("GetUserInfoAsync is implemented in B2.2.");
}
