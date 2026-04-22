// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class DifficultyLabelTests
{
    [Theory]
    [InlineData("MYTHIC_KEYSTONE", 5, ActivityKind.Dungeon, "M+")]
    [InlineData("MYTHIC_KEYSTONE", 0, ActivityKind.Dungeon, "M+")]
    [InlineData("MYTHIC_KEYSTONE", 5, ActivityKind.Raid, "M+")] // tolerates miscategorisation
    public void MythicKeystone_always_renders_as_Mplus(string d, int size, ActivityKind a, string expected)
    {
        Assert.Equal(expected, DifficultyLabel.Format(d, size, a));
    }

    [Theory]
    [InlineData("LFR", 30, ActivityKind.Raid, "LFR (30)")]
    [InlineData("NORMAL", 30, ActivityKind.Raid, "Normal (30)")]
    [InlineData("HEROIC", 30, ActivityKind.Raid, "Heroic (30)")]
    [InlineData("MYTHIC", 20, ActivityKind.Raid, "Mythic (20)")]
    public void Raid_difficulties_append_player_count(string d, int size, ActivityKind a, string expected)
    {
        Assert.Equal(expected, DifficultyLabel.Format(d, size, a));
    }

    [Theory]
    [InlineData("NORMAL", 5, ActivityKind.Dungeon, "Normal")]
    [InlineData("HEROIC", 5, ActivityKind.Dungeon, "Heroic")]
    [InlineData("MYTHIC", 5, ActivityKind.Dungeon, "Mythic")]
    public void Dungeon_difficulties_omit_the_always_5_player_count(string d, int size, ActivityKind a, string expected)
    {
        Assert.Equal(expected, DifficultyLabel.Format(d, size, a));
    }

    [Fact]
    public void Empty_difficulty_returns_empty_string()
    {
        Assert.Equal("", DifficultyLabel.Format("", 25, ActivityKind.Raid));
    }

    [Fact]
    public void Unknown_difficulty_title_cases_the_wire_token()
    {
        // Defensive: Blizzard could ship a new difficulty type we don't know yet.
        // The WoW difficulty words are canonical English brand terms, so we
        // title-case the wire token rather than translate — same rule as LFR /
        // NORMAL / HEROIC / MYTHIC above, which are intentionally not localised.
        Assert.Equal("Ascended", DifficultyLabel.Format("ASCENDED", 0, ActivityKind.Raid));
    }
}
