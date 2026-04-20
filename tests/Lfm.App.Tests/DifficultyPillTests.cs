// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Lfm.App.Components;
using Xunit;

namespace Lfm.App.Tests;

/// <summary>
/// Locks the <see cref="DifficultyPill"/> label + CSS-class mapping.
///
/// The mapping is load-bearing for two reasons:
///   1. The visible label is what users read in the run list — "M+" is
///      short enough to fit beside a title in a narrow sidebar, whereas
///      the raw "MYTHIC_KEYSTONE" difficulty string wrapped awkwardly.
///   2. The CSS class suffix (`difficulty-pill--{variant}`) is the contract
///      with `app.css` styling + with <see cref="DifficultyPillContrastTests"/>.
/// </summary>
public class DifficultyPillTests : ComponentTestBase
{
    [Theory]
    [InlineData("MYTHIC_KEYSTONE", "M+", "mplus")]
    [InlineData("mythic_keystone:5", "M+", "mplus")]
    [InlineData("MYTHIC", "Mythic", "mythic")]
    [InlineData("MYTHIC:25", "Mythic", "mythic")]
    [InlineData("HEROIC", "Heroic", "heroic")]
    [InlineData("NORMAL", "Normal", "normal")]
    [InlineData("LFR", "LFR", "lfr")]
    public void Renders_Expected_Label_And_Class(string modeKey, string expectedLabel, string expectedVariant)
    {
        var cut = Render<DifficultyPill>(p => p.Add(x => x.ModeKey, modeKey));

        var pill = cut.Find(".difficulty-pill");
        Assert.Contains(expectedLabel, pill.TextContent);
        Assert.Contains($"difficulty-pill--{expectedVariant}", pill.ClassName);
    }

    [Fact]
    public void MythicKeystone_Does_Not_Leak_Raw_Enum_Text()
    {
        var cut = Render<DifficultyPill>(p => p.Add(x => x.ModeKey, "MYTHIC_KEYSTONE"));

        var pill = cut.Find(".difficulty-pill");
        Assert.DoesNotContain("keystone", pill.TextContent, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("_", pill.TextContent);
    }

    [Fact]
    public void Unknown_Difficulty_Falls_Back_To_TitleCased_Label()
    {
        var cut = Render<DifficultyPill>(p => p.Add(x => x.ModeKey, "PVP"));

        var pill = cut.Find(".difficulty-pill");
        // Title-case rule: first letter upper, rest lower (invariant) — not
        // CultureInfo.TextInfo.ToTitleCase, which is locale-sensitive.
        Assert.Equal("Pvp", pill.TextContent.Trim());
        Assert.Contains("difficulty-pill--unknown", pill.ClassName);
    }
}
