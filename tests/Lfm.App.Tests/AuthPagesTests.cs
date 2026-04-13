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
    public void LandingPage_Renders_Title_Subtitle_And_SignIn_Cta()
    {
        var cut = Render<LandingPage>();

        cut.Markup.Should().Contain(Loc("landing.title"));
        cut.Markup.Should().Contain(Loc("landing.subtitle"));
        cut.Markup.Should().Contain(Loc("landing.signIn"));
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
    public void LoginFailedPage_Renders_Title_Subtitle_And_Retry_Button()
    {
        var cut = Render<LoginFailedPage>();

        cut.Markup.Should().Contain(Loc("loginFailed.title"));
        cut.Markup.Should().Contain(Loc("loginFailed.subtitle"));
        cut.Markup.Should().Contain(Loc("loginFailed.button"));
    }

    [Fact]
    public void GoodbyePage_Renders_Title_And_SignIn_Cta()
    {
        var cut = Render<GoodbyePage>();

        cut.Markup.Should().Contain(Loc("goodbye.title"));
        cut.Markup.Should().Contain(Loc("goodbye.body1"));
        cut.Markup.Should().Contain(Loc("goodbye.signIn"));
    }

    [Fact]
    public void PrivacyPolicyPage_Renders_All_Section_Headings()
    {
        var cut = Render<PrivacyPolicyPage>();

        cut.Markup.Should().Contain(Loc("privacy.title"));
        cut.Markup.Should().Contain(Loc("privacy.controller.heading"));
        cut.Markup.Should().Contain(Loc("privacy.data.heading"));
        cut.Markup.Should().Contain(Loc("privacy.cookies.heading"));
        cut.Markup.Should().Contain(Loc("privacy.thirdParty.heading"));
        cut.Markup.Should().Contain(Loc("privacy.retention.heading"));
        cut.Markup.Should().Contain(Loc("privacy.rights.heading"));
        cut.Markup.Should().Contain(Loc("privacy.contact.heading"));
    }
}
