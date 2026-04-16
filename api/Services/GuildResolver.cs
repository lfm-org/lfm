// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

public static class GuildResolver
{
    public static (string? GuildId, string? GuildName) FromRaider(RaiderDocument? raider)
    {
        if (raider?.Characters is null || raider.SelectedCharacterId is null)
            return (null, null);

        var selected = raider.Characters.FirstOrDefault(
            c => c.Id == raider.SelectedCharacterId);
        if (selected?.GuildId is null)
            return (null, null);

        return (selected.GuildId.Value.ToString(), selected.GuildName);
    }
}
