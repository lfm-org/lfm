// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Characters;

namespace Lfm.App.Services;

/// <summary>
/// Result of <see cref="IBattleNetClient.GetCharactersAsync"/>.  The backend
/// returns 204 No Content when no cached account profile summary exists (or the
/// 15-minute cooldown has expired), signalling that the caller must POST to
/// /api/battlenet/characters/refresh before characters can be shown.  That
/// signal is distinct from a transport/server error, so callers must handle it
/// separately from an error.
/// </summary>
public abstract record CharactersFetchResult
{
    public sealed record Cached(IReadOnlyList<CharacterDto> Characters) : CharactersFetchResult;

    public sealed record NeedsRefresh : CharactersFetchResult;

    public sealed record Error : CharactersFetchResult;
}
