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
        var cut = RenderComponent<LandingPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void LoginPage_Renders_Without_Crash()
    {
        var cut = RenderComponent<LoginPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void LoginSuccessPage_Renders_And_Redirects_To_Default()
    {
        var nav = Services.GetRequiredService<FakeNavigationManager>();

        RenderComponent<LoginSuccessPage>();

        nav.History.Should().ContainSingle(e => e.Uri.EndsWith("/runs"));
    }

    [Fact]
    public void LoginSuccessPage_Renders_And_Redirects_To_Redirect_Param()
    {
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("/login/success?redirect=%2Fguild");

        RenderComponent<LoginSuccessPage>();

        nav.History.Should().Contain(e => e.Uri.EndsWith("/guild"));
    }

    [Fact]
    public void LoginFailedPage_Renders_Without_Crash()
    {
        var cut = RenderComponent<LoginFailedPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void GoodbyePage_Renders_Without_Crash()
    {
        var cut = RenderComponent<GoodbyePage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void PrivacyPolicyPage_Renders_Without_Crash()
    {
        var cut = RenderComponent<PrivacyPolicyPage>();

        cut.Markup.Should().NotBeEmpty();
    }
}
