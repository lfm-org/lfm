using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Lfm.App.Pages;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lfm.App.Tests;

public class AuthPagesTests : ComponentTestBase
{
    [Fact]
    public void LandingPage_Renders_Without_Crash()
    {
        var cut = Render<LandingPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void LoginPage_Button_Navigates_To_BattleNet_Login_With_Default_Redirect()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<LoginPage>();

        cut.Find("fluent-button").Click();

        var entry = nav.History.Should().ContainSingle().Subject;
        entry.Uri.Should().StartWith("http://localhost:7071/api/battlenet/login");
        entry.Uri.Should().Contain("redirect=%2Fruns");
        entry.Options.ForceLoad.Should().BeTrue();
    }

    [Fact]
    public void LoginPage_Button_Navigates_With_Custom_Redirect()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/login?redirect=%2Fguild");

        var cut = Render<LoginPage>();

        cut.Find("fluent-button").Click();

        var forceEntry = nav.History.Should().Contain(e => e.Options.ForceLoad).Subject;
        forceEntry.Uri.Should().Contain("redirect=%2Fguild");
    }

    [Fact]
    public void LoginSuccessPage_Renders_And_Redirects_To_Default()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        Render<LoginSuccessPage>();

        nav.History.Should().ContainSingle(e => e.Uri.EndsWith("/runs"));
    }

    [Fact]
    public void LoginSuccessPage_Renders_And_Redirects_To_Redirect_Param()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/login/success?redirect=%2Fguild");

        Render<LoginSuccessPage>();

        nav.History.Should().Contain(e => e.Uri.EndsWith("/guild"));
    }

    [Fact]
    public void LoginFailedPage_Renders_Without_Crash()
    {
        var cut = Render<LoginFailedPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void GoodbyePage_Renders_Without_Crash()
    {
        var cut = Render<GoodbyePage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void PrivacyPolicyPage_Renders_Without_Crash()
    {
        var cut = Render<PrivacyPolicyPage>();

        cut.Markup.Should().NotBeEmpty();
    }
}
