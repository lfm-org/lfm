using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

/// <summary>
/// Typed HTTP client for the Blizzard Game Data / Profile APIs.
/// Used by the battlenet-characters-refresh endpoint (B2.5) and portrait refresh (B2.6).
/// </summary>
public interface IBlizzardProfileClient
{
    /// <summary>
    /// Fetches the WoW account profile summary for the authenticated user.
    /// </summary>
    /// <param name="accessToken">Battle.net OAuth access token (from the raider's session).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw Blizzard account profile summary.</returns>
    /// <exception cref="HttpRequestException">
    /// Thrown when the Blizzard API returns a non-success status code.
    /// </exception>
    Task<BlizzardAccountProfileSummary> GetAccountProfileSummaryAsync(string accessToken, CancellationToken ct);
}
