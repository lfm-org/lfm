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
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        // Passthrough localizer returns the key — "nav.logo" renders as "nav.logo"
        cut.Markup.Should().Contain("nav.logo");
    }

    [Fact]
    public void MainLayout_Shows_SignIn_When_Unauthenticated()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("nav.signIn"));
    }

    [Fact]
    public void MainLayout_Shows_Nav_Links_When_Authenticated()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("nav.runs");
            cut.Markup.Should().Contain("nav.guild");
            cut.Markup.Should().Contain("nav.characters");
        });
    }

    [Fact]
    public void MainLayout_Shows_Sign_Out_When_Authenticated()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("nav.signOut"));
    }

    [Fact]
    public void MainLayout_Renders_Body_Content()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "unique-page-marker")));

        cut.Markup.Should().Contain("unique-page-marker");
    }

    [Fact]
    public void MainLayout_Renders_Footer_With_Language_Toggle()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.Markup.Should().Contain("footer.privacy");
        cut.Markup.Should().Contain("locale.en");
        cut.Markup.Should().Contain("locale.fi");
    }

    [Fact]
    public void MainLayout_Renders_Skip_To_Content_Link()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.Markup.Should().Contain("nav.skipToContent");
    }
}
