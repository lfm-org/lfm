// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Lfm.App.Layout;
using Lfm.App.Services;
using Microsoft.Extensions.DependencyInjection;
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

        Assert.Contains(Loc("nav.logo"), cut.Markup);
    }

    [Fact]
    public void MainLayout_Shows_SignIn_When_Unauthenticated()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => Assert.Contains(Loc("nav.signIn"), cut.Markup));
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
            Assert.Contains(Loc("nav.runs"), cut.Markup);
            Assert.Contains(Loc("nav.guild"), cut.Markup);
            Assert.Contains(Loc("nav.characters"), cut.Markup);
        });
    }

    [Fact]
    public void MainLayout_Shows_Sign_Out_When_Authenticated()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => Assert.Contains(Loc("nav.signOut"), cut.Markup));
    }

    [Fact]
    public void MainLayout_Renders_Body_Content()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "unique-page-marker")));

        Assert.Contains("unique-page-marker", cut.Markup);
    }

    [Fact]
    public void MainLayout_Renders_Footer_With_Language_Toggle()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        Assert.Contains(Loc("footer.privacyPolicy"), cut.Markup);
        Assert.Contains(Loc("locale.en"), cut.Markup);
        Assert.Contains(Loc("locale.fi"), cut.Markup);
    }

    [Fact]
    public void MainLayout_Renders_Skip_To_Content_Link()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        Assert.Contains(Loc("a11y.skipToContent"), cut.Markup);
    }

    [Fact]
    public void MainLayout_Renders_Theme_Toggle_Button()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        var toggle = cut.Find("fluent-button[aria-label*='mode']");
        Assert.NotNull(toggle);
    }

    [Fact]
    public void MainLayout_Default_Dark_Mode_Shows_Switch_To_Light_Tooltip()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        var toggle = cut.Find("fluent-button[aria-label='Switch to light mode']");
        Assert.NotNull(toggle);
    }

    [Fact]
    public void MainLayout_Main_Element_Has_Ref_For_Focus_Management()
    {
        this.AddAuthorization();
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        // The <main> must exist and be focusable (tabindex=-1).
        var main = cut.Find("main#main-content");
        Assert.NotNull(main);
        Assert.Equal("-1", main.GetAttribute("tabindex"));
        // The FocusOnNavigate component with Selector="h1" must be gone — focus is now driven by MainLayout's LocationChanged handler.
        Assert.DoesNotContain("FocusOnNavigate", cut.Markup);
    }
}
