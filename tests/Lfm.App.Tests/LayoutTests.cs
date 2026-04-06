using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Lfm.App.Layout;
using Xunit;

namespace Lfm.App.Tests;

public class LayoutTests : ComponentTestBase
{
    [Fact]
    public void MainLayout_Renders_App_Name()
    {
        this.AddTestAuthorization();
        var cut = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.Markup.Should().Contain("LFM");
    }

    [Fact]
    public void MainLayout_Shows_SignIn_When_Unauthenticated()
    {
        this.AddTestAuthorization();
        var cut = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sign In"));
    }

    [Fact]
    public void MainLayout_Shows_Nav_Links_When_Authenticated()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Runs");
            cut.Markup.Should().Contain("Guild");
            cut.Markup.Should().Contain("Characters");
        });
    }

    [Fact]
    public void MainLayout_Shows_Sign_Out_When_Authenticated()
    {
        var auth = this.AddTestAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Sign Out"));
    }

    [Fact]
    public void MainLayout_Renders_Body_Content()
    {
        this.AddTestAuthorization();
        var cut = RenderComponent<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "unique-page-marker")));

        cut.Markup.Should().Contain("unique-page-marker");
    }
}
