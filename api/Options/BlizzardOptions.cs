// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class BlizzardOptions
{
    public const string SectionName = "Blizzard";
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string Region { get; init; }
    public required string RedirectUri { get; init; }
    public required string AppBaseUrl { get; init; }

    /// <summary>
    /// Optional override for the Battle.net OAuth host. When set, replaces
    /// <c>https://{region}.battle.net</c> in the authorize / token / userinfo
    /// URLs the client constructs. Unset in production; set in E2E tests so
    /// the OAuth flow can be exercised against a local stub server without
    /// touching the real Battle.net endpoints.
    /// </summary>
    public string? OAuthBaseUrl { get; init; }

    /// <summary>
    /// Optional full authorize endpoint override. Unset in production; set in E2E
    /// when the browser must navigate to a Testcontainers-managed OAuth provider.
    /// </summary>
    public string? AuthorizationEndpoint { get; init; }

    /// <summary>
    /// Optional full token endpoint override. Unset in production; set in E2E when
    /// the API container must call a provider through Docker host networking.
    /// </summary>
    public string? TokenEndpoint { get; init; }

    /// <summary>
    /// Optional full userinfo endpoint override. Unset in production; set in E2E
    /// when the API container must call a provider through Docker host networking.
    /// </summary>
    public string? UserInfoEndpoint { get; init; }
}
