// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Lfm.App.Auth;
using Lfm.App.Layout;
using Lfm.App.Services;
using Lfm.App.i18n;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
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
    public void MainLayout_Desktop_Nav_Is_A_Plain_Div_So_Display_None_Wins_On_Mobile()
    {
        // The desktop nav must be a plain <div class="desktop-nav"> — wrapping
        // it in a FluentStack injects inline display:flex, which would outrank
        // the `.desktop-nav { display: none }` rule at narrow viewports and
        // cause both the desktop links and the hamburger to render at the same
        // time on mobile (the bug the screenshot was pointing at).
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            var desktopNav = cut.Find("div.desktop-nav");
            Assert.Equal("div", desktopNav.TagName, ignoreCase: true);
        });
    }

    [Fact]
    public void MainLayout_Shows_Mobile_Nav_Toggle_Button_When_Authenticated()
    {
        // The hamburger is what users tap on narrow viewports once the
        // desktop nav hides. If the toggle disappears, navigation is stranded.
        // Asserting `[aria-label]` presence guards against a regression where
        // the glyph renders but the button is unlabelled to assistive tech.
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            var toggle = cut.Find(".mobile-nav-toggle");
            Assert.NotNull(toggle);
            Assert.False(string.IsNullOrWhiteSpace(toggle.GetAttribute("aria-label")));
        });
    }

    [Fact]
    public void MainLayout_Renders_Account_Menu_Button_With_Selected_Character_Name()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        auth.SetClaims(
            new Claim(AppAuthenticationStateProvider.SelectedCharacterNameClaim, "Aelrin"),
            new Claim(
                AppAuthenticationStateProvider.SelectedCharacterPortraitUrlClaim,
                "https://render.worldofwarcraft.com/eu/aelrin-avatar.jpg"));

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            var trigger = cut.Find("fluent-button.account-menu-trigger");
            Assert.Contains("Aelrin", trigger.TextContent);
            Assert.Equal("Account menu for Aelrin", trigger.GetAttribute("aria-label"));
            Assert.Contains("https://render.worldofwarcraft.com/eu/aelrin-avatar.jpg", cut.Markup);
        });
    }

    [Fact]
    public void MainLayout_Account_Disclosure_Does_Not_Advertise_Menu_Pattern()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        auth.SetClaims(new Claim(AppAuthenticationStateProvider.SelectedCharacterNameClaim, "Aelrin"));

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            var trigger = cut.Find("fluent-button.account-menu-trigger");
            Assert.Null(trigger.GetAttribute("aria-haspopup"));
            Assert.Empty(cut.FindAll("[role='menu']"));
            Assert.Empty(cut.FindAll("[role='menuitem']"));
        });
    }

    [Fact]
    public void MainLayout_Keeps_Theme_Locale_Source_And_Sign_Out_Controls_With_Account_Menu()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        auth.SetClaims(new Claim(AppAuthenticationStateProvider.SelectedCharacterNameClaim, "Aelrin"));

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find(".mobile-nav-toggle"));
            Assert.NotNull(cut.Find("fluent-button[aria-label='Switch to light mode']"));
            Assert.Contains(Loc("footer.source"), cut.Markup);
            Assert.Contains(Loc("locale.en"), cut.Markup);
            Assert.Contains(Loc("locale.fi"), cut.Markup);
            Assert.Contains(Loc("nav.signOut"), cut.Markup);
        });
    }

    [Fact]
    public void MainLayout_Account_Menu_Hides_Admin_Item_For_Non_Site_Admins()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        auth.SetClaims(new Claim(AppAuthenticationStateProvider.SelectedCharacterNameClaim, "Aelrin"));

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => Assert.DoesNotContain("/admin/reference", cut.Markup));
    }

    [Fact]
    public void MainLayout_Account_Menu_Shows_Admin_Item_For_Site_Admins()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("admin#1234");
        auth.SetRoles("SiteAdmin");
        auth.SetClaims(new Claim(AppAuthenticationStateProvider.SelectedCharacterNameClaim, "Aelrin"));

        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        cut.WaitForAssertion(() => Assert.Contains("/admin/reference", cut.Markup));
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

    [Fact]
    public void MainLayout_Locale_Change_Survives_JSDisconnectedException()
    {
        // Regression: HandleLocaleChanged is `async void` and crosses JS interop.
        // An unhandled JSDisconnectedException during teardown would escape to
        // the SynchronizationContext and crash the WASM circuit. The handler
        // must catch it.
        this.AddAuthorization();
        // Render first (under default Loose JSInterop, the initial-render
        // lfmSetDocumentLang call succeeds), then arm the exception for the
        // subsequent locale-change call.
        var cut = Render<MainLayout>(p =>
            p.Add(x => x.Body, builder => builder.AddContent(0, "page content")));

        JSInterop.SetupVoid("lfmSetDocumentLang", "fi")
            .SetException(new Microsoft.JSInterop.JSDisconnectedException("test: channel disposed"));

        var localeService = Services.GetRequiredService<ILocaleService>();

        // Triggering the locale change fires HandleLocaleChanged. The handler
        // is async void; without the try/catch, the JSDisconnectedException
        // raised by the test's strict JSInterop setup propagates out of the
        // event invocation. With the fix it is swallowed.
        Exception? caught = null;
        try
        {
            localeService.SetLocale("fi");
        }
        catch (Exception ex)
        {
            caught = ex;
        }
        Assert.Null(caught);
    }
}
