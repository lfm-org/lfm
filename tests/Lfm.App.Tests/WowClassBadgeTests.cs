// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Lfm.App.Components;
using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.App.Tests;

public class WowClassBadgeTests : ComponentTestBase
{
    [Theory]
    [InlineData(1)]    // Warrior
    [InlineData(6)]    // Death Knight
    [InlineData(11)]   // Druid
    public void Renders_Span_With_Correct_Class_Color(int classId)
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, classId)
            .Add(x => x.CharacterName, "TestChar"));

        var span = cut.Find("span");
        Assert.Contains($"color:{WowClasses.GetColor(classId)}", span.GetAttribute("style"));
        Assert.Equal("TestChar", span.TextContent);
    }

    [Fact]
    public void Renders_White_For_Unknown_Class_Id()
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, 999)
            .Add(x => x.CharacterName, "Unknown"));

        var span = cut.Find("span");
        Assert.Contains($"color:{WowClasses.GetColor(999)}", span.GetAttribute("style"));
    }

    [Fact]
    public void Renders_Bold_Font_Weight()
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, 8)
            .Add(x => x.CharacterName, "Frostbolt"));

        var span = cut.Find("span");
        Assert.Contains("font-weight:600", span.GetAttribute("style"));
    }
}
