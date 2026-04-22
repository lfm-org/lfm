// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

/// <summary>
/// Maps Blizzard's wire-format difficulty strings to the label the create-run
/// form shows. Returns English because the WoW difficulty words are canonical
/// brand terms (raid leaders say "Heroic Amirdrassil" regardless of locale);
/// translating them would confuse Finnish / German / Spanish players who have
/// always read them as English. Compare "Battlegrounds" / "Dungeon Finder".
/// </summary>
public static class DifficultyLabel
{
    /// <summary>
    /// Short label for a ToggleGroup option. For M+ always returns "M+"
    /// regardless of size — the key level is a separate control.
    /// </summary>
    public static string Format(string difficultyId, int size, ActivityKind activity)
    {
        if (string.IsNullOrEmpty(difficultyId)) return "";

        if (difficultyId == "MYTHIC_KEYSTONE") return "M+";

        var word = difficultyId switch
        {
            "LFR" => "LFR",
            "NORMAL" => "Normal",
            "HEROIC" => "Heroic",
            "MYTHIC" => "Mythic",
            _ => ToTitleCase(difficultyId),
        };

        // Raids benefit from showing the player count alongside the
        // difficulty; dungeons are always 5, so the number is noise there.
        return activity == ActivityKind.Raid && size > 0
            ? $"{word} ({size})"
            : word;
    }

    private static string ToTitleCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var lower = s.Replace('_', ' ').ToLowerInvariant();
        return char.ToUpperInvariant(lower[0]) + lower[1..];
    }
}
