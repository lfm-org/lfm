using FluentAssertions;
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
        WowClasses.GetName(classId).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllClassColors))]
    public void GetColor_returns_correct_color_for_all_classes(int classId, string expected)
    {
        WowClasses.GetColor(classId).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(14)]
    public void GetName_returns_Unknown_for_invalid_classId(int classId)
    {
        WowClasses.GetName(classId).Should().Be("Unknown");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    [InlineData(14)]
    public void GetColor_returns_white_for_invalid_classId(int classId)
    {
        WowClasses.GetColor(classId).Should().Be("#FFFFFF");
    }

    [Fact]
    public void Names_dictionary_contains_exactly_13_entries()
    {
        WowClasses.Names.Should().HaveCount(13);
    }

    [Fact]
    public void Colors_dictionary_contains_exactly_13_entries()
    {
        WowClasses.Colors.Should().HaveCount(13);
    }
}
