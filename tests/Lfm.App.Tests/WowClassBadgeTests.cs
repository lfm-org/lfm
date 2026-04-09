using Bunit;
using FluentAssertions;
using Lfm.App.Components;
using Xunit;

namespace Lfm.App.Tests;

public class WowClassBadgeTests : ComponentTestBase
{
    [Theory]
    [InlineData(1, "#C69B6D")]   // Warrior
    [InlineData(6, "#C41E3A")]   // Death Knight
    [InlineData(11, "#FF7C0A")]  // Druid
    public void Renders_Span_With_Correct_Class_Color(int classId, string expectedColor)
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, classId)
            .Add(x => x.CharacterName, "TestChar"));

        var span = cut.Find("span");
        span.GetAttribute("style").Should().Contain($"color:{expectedColor}");
        span.TextContent.Should().Be("TestChar");
    }

    [Fact]
    public void Renders_White_For_Unknown_Class_Id()
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, 999)
            .Add(x => x.CharacterName, "Unknown"));

        var span = cut.Find("span");
        span.GetAttribute("style").Should().Contain("color:#FFFFFF");
    }

    [Fact]
    public void Renders_Bold_Font_Weight()
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, 8)
            .Add(x => x.CharacterName, "Frostbolt"));

        var span = cut.Find("span");
        span.GetAttribute("style").Should().Contain("font-weight:600");
    }
}
