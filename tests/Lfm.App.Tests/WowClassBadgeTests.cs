using Bunit;
using FluentAssertions;
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
        span.GetAttribute("style").Should().Contain($"color:{WowClasses.GetColor(classId)}");
        span.TextContent.Should().Be("TestChar");
    }

    [Fact]
    public void Renders_White_For_Unknown_Class_Id()
    {
        var cut = Render<WowClassBadge>(p => p
            .Add(x => x.ClassId, 999)
            .Add(x => x.CharacterName, "Unknown"));

        var span = cut.Find("span");
        span.GetAttribute("style").Should().Contain($"color:{WowClasses.GetColor(999)}");
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
