using FluentAssertions;
using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.App.Tests;

public class WowClassesTests
{
    [Theory]
    [InlineData(1, "#C69B6D")]
    [InlineData(2, "#F48CBA")]
    [InlineData(3, "#AAD372")]
    [InlineData(4, "#FFF468")]
    [InlineData(5, "#FFFFFF")]
    [InlineData(6, "#C41E3A")]
    [InlineData(7, "#0070DD")]
    [InlineData(8, "#3FC7EB")]
    [InlineData(9, "#8788EE")]
    [InlineData(10, "#00FF98")]
    [InlineData(11, "#FF7C0A")]
    [InlineData(12, "#A330C9")]
    [InlineData(13, "#33937F")]
    public void GetColor_Returns_Correct_Color_For_Known_Classes(int classId, string expected)
    {
        WowClasses.GetColor(classId).Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(14)]
    [InlineData(999)]
    public void GetColor_Returns_White_For_Unknown_Classes(int classId)
    {
        WowClasses.GetColor(classId).Should().Be("#FFFFFF");
    }
}
