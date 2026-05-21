// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.WoW;

/// <summary>
/// WoW class colour lookup used for UI rendering of character names.
/// Class IDs follow the Blizzard API playable-class index.
/// </summary>
public static class WowClasses
{
    private static readonly IReadOnlyDictionary<int, string> Names =
        new Dictionary<int, string>
        {
            [1] = "Warrior",
            [2] = "Paladin",
            [3] = "Hunter",
            [4] = "Rogue",
            [5] = "Priest",
            [6] = "Death Knight",
            [7] = "Shaman",
            [8] = "Mage",
            [9] = "Warlock",
            [10] = "Monk",
            [11] = "Druid",
            [12] = "Demon Hunter",
            [13] = "Evoker",
        };

    private static readonly IReadOnlyDictionary<int, string> Colors =
        new Dictionary<int, string>
        {
            [1] = "#C69B6D",   // Warrior
            [2] = "#F48CBA",   // Paladin
            [3] = "#AAD372",   // Hunter
            [4] = "#FFF468",   // Rogue
            [5] = "#FFFFFF",   // Priest
            [6] = "#C41E3A",   // Death Knight
            [7] = "#0070DD",   // Shaman
            [8] = "#3FC7EB",   // Mage
            [9] = "#8788EE",   // Warlock
            [10] = "#00FF98",  // Monk
            [11] = "#FF7C0A",  // Druid
            [12] = "#A330C9",  // Demon Hunter
            [13] = "#33937F",  // Evoker
        };

    private static readonly IReadOnlyDictionary<int, string> IconNames =
        new Dictionary<int, string>
        {
            [1] = "classicon_warrior",
            [2] = "classicon_paladin",
            [3] = "classicon_hunter",
            [4] = "classicon_rogue",
            [5] = "classicon_priest",
            [6] = "spell_deathknight_classicon",
            [7] = "classicon_shaman",
            [8] = "classicon_mage",
            [9] = "classicon_warlock",
            [10] = "classicon_monk",
            [11] = "classicon_druid",
            [12] = "classicon_demonhunter",
            [13] = "classicon_evoker",
        };

    /// <summary>
    /// Returns the English name for a WoW class ID, or "Unknown" for unknown IDs.
    /// </summary>
    public static string GetName(int classId) =>
        Names.TryGetValue(classId, out var name) ? name : "Unknown";

    /// <summary>
    /// Returns the hex colour for a WoW class ID, or white for unknown IDs.
    /// </summary>
    public static string GetColor(int classId) =>
        Colors.TryGetValue(classId, out var color) ? color : "#FFFFFF";

    /// <summary>
    /// Returns the render asset path for a WoW class ID, or null for unknown IDs.
    /// Callers that render this as an image must route it through the app media
    /// cache instead of linking directly to Blizzard.
    /// </summary>
    public static string? GetIconPath(int classId) =>
        IconNames.TryGetValue(classId, out var iconName) ? $"icons/56/{iconName}.jpg" : null;
}
