// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Services;

internal static class RunAccessPolicy
{
    internal static bool CanView(RunDocument run, string currentBattleNetId, string? callerGuildId) =>
        run.Visibility != "GUILD"
        || IsCreator(run, currentBattleNetId)
        || IsSameGuild(run, callerGuildId);

    internal static bool IsGuildPeer(RunDocument run, string currentBattleNetId, string? callerGuildId) =>
        run.Visibility == "GUILD"
        && !IsCreator(run, currentBattleNetId)
        && IsSameGuild(run, callerGuildId);

    internal static bool IsCreator(RunDocument run, string currentBattleNetId) =>
        run.CreatorBattleNetId == currentBattleNetId;

    private static bool IsSameGuild(RunDocument run, string? callerGuildId) =>
        callerGuildId is not null
        && run.CreatorGuildId is not null
        && run.CreatorGuildId.ToString() == callerGuildId;
}
