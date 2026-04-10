using FluentAssertions;
using Lfm.App.i18n;
using Xunit;

namespace Lfm.App.Core.Tests.i18n;

public class LocaleServiceTests
{
    [Fact]
    public void Default_Locale_Is_English()
    {
        var sut = new LocaleService();

        sut.CurrentLocale.Should().Be("en");
    }

    [Fact]
    public void SetLocale_Changes_Current_Locale()
    {
        var sut = new LocaleService();

        sut.SetLocale("fi");

        sut.CurrentLocale.Should().Be("fi");
    }

    [Fact]
    public void SetLocale_Fires_OnLocaleChanged()
    {
        var sut = new LocaleService();
        var fired = false;
        sut.OnLocaleChanged += () => fired = true;

        sut.SetLocale("fi");

        fired.Should().BeTrue();
    }

    [Fact]
    public void SetLocale_Does_Not_Fire_When_Already_Active()
    {
        var sut = new LocaleService();
        var count = 0;
        sut.OnLocaleChanged += () => count++;

        sut.SetLocale("en"); // already "en"

        count.Should().Be(0);
    }

    [Fact]
    public void SetLocale_Rejects_Invalid_Locale()
    {
        var sut = new LocaleService();
        var fired = false;
        sut.OnLocaleChanged += () => fired = true;

        sut.SetLocale("de");

        sut.CurrentLocale.Should().Be("en");
        fired.Should().BeFalse();
    }

    [Fact]
    public void SetLocale_Is_Case_Insensitive()
    {
        var sut = new LocaleService();

        sut.SetLocale("FI");

        sut.CurrentLocale.Should().Be("fi");
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
            sut.CurrentLocale.Should().Be(locale.ToLowerInvariant());
        else
            sut.CurrentLocale.Should().Be(defaultLocale, "unsupported locales must not change the active locale");
    }
}
