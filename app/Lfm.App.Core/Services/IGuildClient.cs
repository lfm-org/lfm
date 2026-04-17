// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public interface IGuildClient
{
    Task<GuildDto?> GetAsync(CancellationToken ct);
    Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct);
}
