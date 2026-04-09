using FluentAssertions;
using Lfm.App.Services;
using Microsoft.FluentUI.AspNetCore.Components;
using Xunit;

namespace Lfm.App.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void Default_Mode_Is_Dark()
    {
        var sut = new ThemeService();

        sut.Mode.Should().Be(DesignThemeModes.Dark);
    }

    [Fact]
    public void Toggle_Switches_Dark_To_Light()
    {
        var sut = new ThemeService();

        sut.Toggle();

        sut.Mode.Should().Be(DesignThemeModes.Light);
    }

    [Fact]
    public void Toggle_Twice_Returns_To_Dark()
    {
        var sut = new ThemeService();

        sut.Toggle();
        sut.Toggle();

        sut.Mode.Should().Be(DesignThemeModes.Dark);
    }

    [Fact]
    public void SetMode_Changes_Mode()
    {
        var sut = new ThemeService();

        sut.SetMode(DesignThemeModes.Light);

        sut.Mode.Should().Be(DesignThemeModes.Light);
    }

    [Fact]
    public void SetMode_Same_Value_Is_NoOp()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Dark);

        changeCount.Should().Be(0);
        sut.Mode.Should().Be(DesignThemeModes.Dark);
    }

    [Fact]
    public void OnChange_Fires_On_Toggle()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.Toggle();

        changeCount.Should().Be(1);
    }

    [Fact]
    public void OnChange_Fires_On_SetMode_With_Different_Value()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Light);

        changeCount.Should().Be(1);
    }

    [Fact]
    public void OnChange_Does_Not_Fire_On_SetMode_With_Same_Value()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Dark);

        changeCount.Should().Be(0);
    }
}
