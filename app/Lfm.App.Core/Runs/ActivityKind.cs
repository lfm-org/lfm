// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

/// <summary>
/// Top-level split for the create-run activity selector. Derived from
/// Blizzard's <c>journal-instance.category.type</c> (<c>RAID</c> / <c>DUNGEON</c>),
/// with <see cref="Other"/> as a defensive fallback for instances whose
/// category isn't one of the known two.
/// </summary>
public enum ActivityKind
{
    Raid,
    Dungeon,
    Other,
}

public static class ActivityKindExtensions
{
    public static ActivityKind FromCategory(string? category) => category switch
    {
        "RAID" => ActivityKind.Raid,
        "DUNGEON" => ActivityKind.Dungeon,
        _ => ActivityKind.Other,
    };
}
