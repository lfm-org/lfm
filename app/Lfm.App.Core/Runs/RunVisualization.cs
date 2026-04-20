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
    public static RunKind GetKind(string? modeKey)
    {
        var (_, size) = RunMode.Parse(modeKey);
        return size switch
        {
            <= 0 => RunKind.Unknown,
            <= 5 => RunKind.Dungeon,
            _ => RunKind.Raid,
        };
    }

    public static string GetKindClass(RunKind kind) => kind switch
    {
        RunKind.Dungeon => "dungeon",
        RunKind.Raid => "raid",
        _ => "unknown",
    };

    public static string GetDifficultyClass(string? modeKey)
    {
        var (difficulty, _) = RunMode.Parse(modeKey);
        return difficulty switch
        {
            "MYTHIC" => "mythic",
            "HEROIC" => "heroic",
            "NORMAL" => "normal",
            "LFR" => "lfr",
            _ => "unknown",
        };
    }

    public static (int Tank, int Healer, int Dps) GetRoleTargets(int size) => size switch
    {
        5 => (1, 1, 3),
        10 => (2, 2, 6),
        20 => (2, 4, 14),
        25 => (2, 5, 18),
        30 => (2, 6, 22),
        _ => (0, 0, 0),
    };

    public static bool IsAttending(string? reviewedAttendance) =>
        reviewedAttendance is "IN" or "LATE" or "BENCH";

    public static string NormalizeRole(string? role) => (role ?? "").ToUpperInvariant() switch
    {
        "TANK" => "TANK",
        "HEALER" or "HEAL" => "HEALER",
        "DPS" or "MELEE" or "RANGED" => "DPS",
        _ => "DPS",
    };

    public static RunRoleCounts CountRoles(IEnumerable<RunCharacterDto> characters, string? modeKey)
    {
        var (_, size) = RunMode.Parse(modeKey);
        var (tTarget, hTarget, dTarget) = GetRoleTargets(size);

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

    // Deterministic 0-359 hue from an instance id. Multiplier chosen to
    // scatter adjacent ids (1, 2, 3…) into visually distinct hues.
    public static int GetInstanceHue(int instanceId)
    {
        var hue = (instanceId * 47) % 360;
        return hue < 0 ? hue + 360 : hue;
    }

    public static string GetInstanceGradient(int instanceId)
    {
        var hue = GetInstanceHue(instanceId);
        return FormattableString.Invariant(
            $"linear-gradient(135deg, oklch(0.55 0.12 {hue} / 0.18), oklch(0.35 0.10 {(hue + 40) % 360} / 0.10))");
    }
}
