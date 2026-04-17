// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Unit tests for <see cref="WowClasses"/> static reference data.
/// </summary>
public class WowClassesTests
{
    public static TheoryData<int, string> AllClassNames => new()
    {
        { 1, "Warrior" },
        { 2, "Paladin" },
        { 3, "Hunter" },
        { 4, "Rogue" },
        { 5, "Priest" },
        { 6, "Death Knight" },
        { 7, "Shaman" },
        { 8, "Mage" },
        { 9, "Warlock" },
        { 10, "Monk" },
        { 11, "Druid" },
        { 12, "Demon Hunter" },
        { 13, "Evoker" },
    };

    public static TheoryData<int, string> AllClassColors => new()
    {
        { 1, "#C69B6D" },
        { 2, "#F48CBA" },
        { 3, "#AAD372" },
        { 4, "#FFF468" },
        { 5, "#FFFFFF" },
        { 6, "#C41E3A" },
        { 7, "#0070DD" },
        { 8, "#3FC7EB" },
        { 9, "#8788EE" },
        { 10, "#00FF98" },
        { 11, "#FF7C0A" },
        { 12, "#A330C9" },
        { 13, "#33937F" },
    };

    [Theory]
    [MemberData(nameof(AllClassNames))]
    public void GetName_returns_correct_name_for_all_classes(int classId, string expected)
    {
        Assert.Equal(expected, WowClasses.GetName(classId));
    }

    [Theory]
    [MemberData(nameof(AllClassColors))]
    public void GetColor_returns_correct_color_for_all_classes(int classId, string expected)
    {
        Assert.Equal(expected, WowClasses.GetColor(classId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(14)]
    public void GetName_returns_Unknown_for_invalid_classId(int classId)
    {
        Assert.Equal("Unknown", WowClasses.GetName(classId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(14)]
    public void GetColor_returns_white_for_invalid_classId(int classId)
    {
        Assert.Equal("#FFFFFF", WowClasses.GetColor(classId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void GetName_returns_non_unknown_for_valid_ids(int classId)
    {
        Assert.NotEqual("Unknown", WowClasses.GetName(classId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    public void GetColor_returns_non_default_for_valid_ids(int classId)
    {
        Assert.NotEqual("#FFFFFF", WowClasses.GetColor(classId));
    }
}
