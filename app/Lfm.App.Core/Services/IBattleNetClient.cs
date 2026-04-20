// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Characters;

namespace Lfm.App.Services;

public interface IBattleNetClient
{
    Task<CharactersFetchResult> GetCharactersAsync(CancellationToken ct);

    Task<IReadOnlyList<CharacterDto>?> RefreshCharactersAsync(CancellationToken ct);

    Task<IDictionary<string, string>?> GetPortraitsAsync(
        IEnumerable<CharacterPortraitRequest> requests,
        CancellationToken ct);
}
