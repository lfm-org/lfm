// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

/// <summary>
/// Maps Blizzard's wire-format difficulty strings and the player-count
/// integer to the user-facing label the create-run form shows. Pure; no
/// i18n — label strings are English-only for now, localisation happens at
/// the Razor boundary via the existing locale JSON files.
/// </summary>
public static class DifficultyLabel
{
    /// <summary>
    /// Short label for a <see cref="ToggleGroup"/> option (Activity = Raid
    /// or Dungeon determines whether Size is shown). For M+, always returns
    /// "M+" regardless of size — the key level is a separate control.
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
