namespace Lfm.Contracts.WoW;

/// <summary>
/// WoW class colour lookup used for UI rendering of character names.
/// Class IDs follow the Blizzard API playable-class index.
/// </summary>
public static class WowClasses
{
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

    /// <summary>
    /// Returns the hex colour for a WoW class ID, or white for unknown IDs.
    /// </summary>
    public static string GetColor(int classId) =>
        Colors.TryGetValue(classId, out var color) ? color : "#FFFFFF";
}
