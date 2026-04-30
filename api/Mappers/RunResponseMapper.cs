// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Helpers;
using Lfm.Api.Repositories;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Mappers;

internal static class RunResponseMapper
{
    internal static RunDetailDto ToDetail(RunDocument run, string currentBattleNetId)
    {
        var (difficulty, size) = RunModeResolver.Resolve(run.Difficulty, run.Size, run.ModeKey);
        return new RunDetailDto(
            Id: run.Id,
            StartTime: run.StartTime,
            SignupCloseTime: run.SignupCloseTime,
            Description: run.Description,
            Visibility: run.Visibility,
            CreatorGuild: run.CreatorGuild,
            InstanceId: run.InstanceId,
            InstanceName: run.InstanceName,
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: run.KeystoneLevel,
            RunCharacters: run.RunCharacters
                .Select(c => ToCharacter(c, currentBattleNetId))
                .ToList());
    }

    internal static RunSummaryDto ToSummary(RunDocument run, string currentBattleNetId)
    {
        var (difficulty, size) = RunModeResolver.Resolve(run.Difficulty, run.Size, run.ModeKey);
        return new RunSummaryDto(
            Id: run.Id,
            StartTime: run.StartTime,
            SignupCloseTime: run.SignupCloseTime,
            Description: run.Description,
            Visibility: run.Visibility,
            CreatorGuild: run.CreatorGuild,
            InstanceId: run.InstanceId,
            InstanceName: run.InstanceName,
            RunCharacters: run.RunCharacters
                .Select(c => ToCharacter(c, currentBattleNetId))
                .ToList(),
            Difficulty: difficulty,
            Size: size,
            KeystoneLevel: run.KeystoneLevel);
    }

    private static RunCharacterDto ToCharacter(
        RunCharacterEntry character,
        string currentBattleNetId) =>
        new(
            CharacterName: character.CharacterName,
            CharacterRealm: character.CharacterRealm,
            CharacterClassId: character.CharacterClassId,
            CharacterClassName: character.CharacterClassName,
            DesiredAttendance: character.DesiredAttendance,
            ReviewedAttendance: character.ReviewedAttendance,
            SpecName: character.SpecName,
            Role: character.Role,
            IsCurrentUser: character.RaiderBattleNetId == currentBattleNetId);
}
