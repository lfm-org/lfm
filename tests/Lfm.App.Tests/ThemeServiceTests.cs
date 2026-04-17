// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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

        Assert.Equal(DesignThemeModes.Dark, sut.Mode);
    }

    [Fact]
    public void Toggle_Switches_Dark_To_Light()
    {
        var sut = new ThemeService();

        sut.Toggle();

        Assert.Equal(DesignThemeModes.Light, sut.Mode);
    }

    [Fact]
    public void Toggle_Twice_Returns_To_Dark()
    {
        var sut = new ThemeService();

        sut.Toggle();
        sut.Toggle();

        Assert.Equal(DesignThemeModes.Dark, sut.Mode);
    }

    [Fact]
    public void SetMode_Changes_Mode()
    {
        var sut = new ThemeService();

        sut.SetMode(DesignThemeModes.Light);

        Assert.Equal(DesignThemeModes.Light, sut.Mode);
    }

    [Fact]
    public void SetMode_Same_Value_Is_NoOp()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Dark);

        Assert.Equal(0, changeCount);
        Assert.Equal(DesignThemeModes.Dark, sut.Mode);
    }

    [Fact]
    public void OnChange_Fires_On_Toggle()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.Toggle();

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void OnChange_Fires_On_SetMode_With_Different_Value()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Light);

        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void OnChange_Does_Not_Fire_On_SetMode_With_Same_Value()
    {
        var sut = new ThemeService();
        var changeCount = 0;
        sut.OnChange += () => changeCount++;

        sut.SetMode(DesignThemeModes.Dark);

        Assert.Equal(0, changeCount);
    }
}
