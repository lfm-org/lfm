// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.Characters;

namespace Lfm.Api.Services;

/// <summary>
/// Resolves portrait URLs for a set of WoW characters.
///
/// Portrait URL resolution order (mirrors battlenet-character-portraits.ts):
///   1. Stored character's portraitUrl (if it is a Blizzard CDN URL).
///   2. Stored character's mediaSummary.assets[key="avatar"].
///   3. The raider's portraitCache map.
///   4. Blizzard character-media API call (requires the user's access token).
///
/// Characters with no resolvable portrait are omitted from the result.
/// If any portraits were resolved or updated, the raider document is persisted.
/// </summary>
public interface ICharacterPortraitService
{
    /// <summary>
    /// Resolves portrait URLs for the given characters.
    /// </summary>
    /// <param name="raider">The raider document (provides cached data and portraitCache).</param>
    /// <param name="requests">Characters to resolve portraits for.</param>
    /// <param name="accessToken">Battle.net OAuth access token for Blizzard API calls.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PortraitResponse"/> mapping character IDs to resolved portrait URLs.
    /// Characters with no resolvable portrait are not included in the result.
    /// </returns>
    Task<PortraitResponse> ResolveAsync(
        RaiderDocument raider,
        IReadOnlyList<CharacterPortraitRequest> requests,
        string accessToken,
        CancellationToken ct);
}
