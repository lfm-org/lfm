// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.i18n;
using Xunit;

namespace Lfm.App.Core.Tests.i18n;

public class LocaleServiceTests
{
    [Fact]
    public void Default_Locale_Is_English()
    {
        var sut = new LocaleService();

        Assert.Equal("en", sut.CurrentLocale);
    }

    [Fact]
    public void SetLocale_Changes_Current_Locale()
    {
        var sut = new LocaleService();

        sut.SetLocale("fi");

        Assert.Equal("fi", sut.CurrentLocale);
    }

    [Fact]
    public void SetLocale_Fires_OnLocaleChanged()
    {
        var sut = new LocaleService();
        var fired = false;
        sut.OnLocaleChanged += () => fired = true;

        sut.SetLocale("fi");

        Assert.True(fired);
    }

    [Fact]
    public void SetLocale_Does_Not_Fire_When_Already_Active()
    {
        var sut = new LocaleService();
        var count = 0;
        sut.OnLocaleChanged += () => count++;

        sut.SetLocale("en"); // already "en"

        Assert.Equal(0, count);
    }

    [Fact]
    public void SetLocale_Rejects_Invalid_Locale()
    {
        var sut = new LocaleService();
        var fired = false;
        sut.OnLocaleChanged += () => fired = true;

        sut.SetLocale("de");

        Assert.Equal("en", sut.CurrentLocale);
        Assert.False(fired);
    }

    [Fact]
    public void SetLocale_Is_Case_Insensitive()
    {
        var sut = new LocaleService();

        sut.SetLocale("FI");

        Assert.Equal("fi", sut.CurrentLocale);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("fi", true)]
    [InlineData("de", false)]
    [InlineData("sv", false)]
    [InlineData("", false)]
    public void Supported_Locale_Set_Is_Exactly_En_And_Fi(string locale, bool shouldBeAccepted)
    {
        var sut = new LocaleService();
        sut.SetLocale("en"); // baseline
        var defaultLocale = sut.CurrentLocale;

        sut.SetLocale(locale);

        if (shouldBeAccepted)
            Assert.Equal(locale.ToLowerInvariant(), sut.CurrentLocale);
        else
            Assert.Equal(defaultLocale, sut.CurrentLocale);
    }
}
