// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Me;

namespace Lfm.App.Services;

public interface IMeClient
{
    Task<MeResponse?> GetAsync(CancellationToken ct);

    Task<UpdateMeResponse?> UpdateAsync(UpdateMeRequest request, CancellationToken ct);

    Task<bool> SelectCharacterAsync(string id, CancellationToken ct);

    Task<bool> DeleteAsync(CancellationToken ct);
}
