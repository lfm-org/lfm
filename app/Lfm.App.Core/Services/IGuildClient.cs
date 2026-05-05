// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public interface IGuildClient
{
    Task<GuildDto?> GetAsync(CancellationToken ct);
    Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct);
    Task<GuildDto?> GetAdminAsync(string guildId, CancellationToken ct);
    Task<GuildDto?> UpdateAdminAsync(string guildId, UpdateGuildRequest request, CancellationToken ct);
}
