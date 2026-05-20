// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using Lfm.Contracts.Runs;

namespace Lfm.App.Runs;

public enum RunKind
{
    Unknown,
    Dungeon,
    Raid,
}

public enum TimeHorizon
{
    Unknown,
    Past,
    ThisWeek,
    NextWeek,
    Later,
}

public readonly record struct RoleCount(int Attending, int Target)
{
    public bool IsShortage => Target > 0 && Attending < Target;
}

public readonly record struct RunRoleCounts(RoleCount Tank, RoleCount Healer, RoleCount Dps);

public static class RunVisualization
{
    private const int MinimumRaidSize = 10;
    private const int MaximumRaidSize = 30;
    private const int MythicRaidSize = 20;
    private const int RaidTankTarget = 2;

    public static RunKind GetKind(int size) => size switch
    {
        <= 0 => RunKind.Unknown,
        <= 5 => RunKind.Dungeon,
        _ => RunKind.Raid,
    };

    public static string GetKindClass(RunKind kind) => kind switch
    {
        RunKind.Dungeon => "dungeon",
        RunKind.Raid => "raid",
        _ => "unknown",
    };

    public static string GetDifficultyClass(string? difficulty) => difficulty switch
    {
        "MYTHIC" => "mythic",
        "HEROIC" => "heroic",
        "NORMAL" => "normal",
        "LFR" => "lfr",
        _ => "unknown",
    };

    public static (int Tank, int Healer, int Dps) GetRoleTargets(int size) =>
        GetRoleTargets(GetKind(size), difficulty: null, size);

    public static (int Tank, int Healer, int Dps) GetRoleTargets(
        RunKind kind,
        string? difficulty,
        int size)
    {
        if (kind == RunKind.Dungeon)
        {
            return (1, 1, 3);
        }

        if (kind != RunKind.Raid)
        {
            return (0, 0, 0);
        }

        var targetSize = difficulty == "MYTHIC" ? MythicRaidSize : size;
        return GetRaidRoleTargets(targetSize);
    }

    private static (int Tank, int Healer, int Dps) GetRaidRoleTargets(int size)
    {
        return size switch
        {
            < MinimumRaidSize or > MaximumRaidSize => (0, 0, 0),
            <= 10 => (RaidTankTarget, 2, 6),
            <= 14 => (RaidTankTarget, 3, 9),
            <= 20 => (RaidTankTarget, 4, 14),
            <= 25 => (RaidTankTarget, 5, 18),
            _ => (RaidTankTarget, 6, 22),
        };
    }

    public static bool IsAttending(string? reviewedAttendance) =>
        reviewedAttendance is "IN" or "LATE" or "BENCH";

    public static string GetAttendanceClass(string? attendance) => attendance switch
    {
        "IN" => "in",
        "LATE" => "late",
        "BENCH" => "bench",
        "OUT" => "out",
        "AWAY" => "away",
        _ => "unknown",
    };

    public static string NormalizeRole(string? role) => (role ?? "").ToUpperInvariant() switch
    {
        "TANK" => "TANK",
        "HEALER" or "HEAL" => "HEALER",
        "DPS" or "MELEE" or "RANGED" => "DPS",
        _ => "DPS",
    };

    public static RunRoleCounts CountRoles(IEnumerable<RunCharacterDto> characters, int size) =>
        CountRoles(characters, GetKind(size), difficulty: null, size);

    public static RunRoleCounts CountRoles(
        IEnumerable<RunCharacterDto> characters,
        RunKind kind,
        string? difficulty,
        int size)
    {
        int tAttend = 0, hAttend = 0, dAttend = 0;
        foreach (var c in characters)
        {
            if (!IsAttending(c.ReviewedAttendance))
            {
                continue;
            }
            switch (NormalizeRole(c.Role))
            {
                case "TANK": tAttend++; break;
                case "HEALER": hAttend++; break;
                default: dAttend++; break;
            }
        }

        var targetSize = difficulty == "MYTHIC"
            ? MythicRaidSize
            : Math.Clamp(tAttend + hAttend + dAttend, MinimumRaidSize, MaximumRaidSize);
        var (tTarget, hTarget, dTarget) = GetRoleTargets(kind, difficulty, targetSize);

        return new RunRoleCounts(
            new RoleCount(tAttend, tTarget),
            new RoleCount(hAttend, hTarget),
            new RoleCount(dAttend, dTarget));
    }

    public static bool IsCurrentUserSignedUp(IEnumerable<RunCharacterDto> characters) =>
        characters.Any(c => c.IsCurrentUser);

    public static TimeHorizon GetHorizon(string? isoStartTime, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(isoStartTime) ||
            !DateTimeOffset.TryParse(isoStartTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start))
        {
            return TimeHorizon.Unknown;
        }

        if (start < now)
        {
            return TimeHorizon.Past;
        }

        var delta = start - now;
        if (delta < TimeSpan.FromDays(7))
        {
            return TimeHorizon.ThisWeek;
        }
        if (delta < TimeSpan.FromDays(14))
        {
            return TimeHorizon.NextWeek;
        }
        return TimeHorizon.Later;
    }
}
