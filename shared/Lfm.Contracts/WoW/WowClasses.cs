namespace Lfm.Contracts.WoW;

/// <summary>
/// Static reference data for WoW character classes.
/// IDs and names match the Blizzard API playable_class values.
/// Colours are the standard class colours used in WoW addons and community sites.
/// </summary>
public static class WowClasses
{
    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
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

    public static readonly IReadOnlyDictionary<int, string> Colors = new Dictionary<int, string>
    {
        [1] = "#C69B6D",
        [2] = "#F48CBA",
        [3] = "#AAD372",
        [4] = "#FFF468",
        [5] = "#FFFFFF",
        [6] = "#C41E3A",
        [7] = "#0070DD",
        [8] = "#3FC7EB",
        [9] = "#8788EE",
        [10] = "#00FF98",
        [11] = "#FF7C0A",
        [12] = "#A330C9",
        [13] = "#33937F",
    };

    public static string GetName(int classId) => Names.GetValueOrDefault(classId, "Unknown");

    public static string GetColor(int classId) => Colors.GetValueOrDefault(classId, "#FFFFFF");
}
