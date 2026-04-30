// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services.Blizzard.Models;

namespace Lfm.Api.Services;

/// <summary>
/// Typed HTTP client for the Blizzard Game Data / Profile APIs.
/// Used by the battlenet-characters-refresh endpoint (B2.5) and portrait refresh (B2.6).
/// Returns Blizzard wire-shape models from <see cref="Blizzard.Models"/>; callers
/// translate to stored Cosmos shapes via
/// <see cref="Blizzard.BlizzardModelTranslator"/> before persisting.
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
    Task<AccountProfileSummaryResponse> GetAccountProfileSummaryAsync(string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a character's profile (name, level, class, race, realm, guild)
    /// from the Blizzard Profile API.
    /// </summary>
    /// <param name="realm">The character's realm slug.</param>
    /// <param name="name">The character's name.</param>
    /// <param name="accessToken">Battle.net OAuth access token (from the raider's session).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">
    /// Thrown when the Blizzard API returns a non-success status code.
    /// </exception>
    Task<CharacterProfileResponse> GetCharacterProfileAsync(
        string realm, string name, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a character's active and available specializations from the Blizzard Profile API.
    /// </summary>
    /// <param name="realm">The character's realm slug.</param>
    /// <param name="name">The character's name.</param>
    /// <param name="accessToken">Battle.net OAuth access token (from the raider's session).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="HttpRequestException">
    /// Thrown when the Blizzard API returns a non-success status code.
    /// </exception>
    Task<CharacterSpecializationsResponse> GetCharacterSpecializationsAsync(
        string realm, string name, string accessToken, CancellationToken ct);

    /// <summary>
    /// Fetches a character's media (portrait, avatar, etc.) from the Blizzard Profile API.
    /// Best-effort: returns null on any non-success HTTP status or transport failure
    /// rather than throwing, so the caller can continue without portrait data.
    /// </summary>
    /// <param name="realm">The character's realm slug.</param>
    /// <param name="name">The character's name.</param>
    /// <param name="accessToken">Battle.net OAuth access token (from the raider's session).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The media summary, or null when the fetch failed for any reason.</returns>
    Task<CharacterMediaSummaryResponse?> GetCharacterMediaAsync(
        string realm, string name, string accessToken, CancellationToken ct);
}
