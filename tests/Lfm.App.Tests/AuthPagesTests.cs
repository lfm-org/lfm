// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
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

        Assert.Contains(Loc("landing.title"), cut.Markup);
        Assert.Contains(Loc("landing.subtitle"), cut.Markup);
        Assert.Contains(Loc("landing.signIn"), cut.Markup);
    }

    [Fact]
    public void LoginPage_Button_Navigates_To_BattleNet_Login_With_Default_Redirect()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<LoginPage>();

        cut.Find("fluent-button").Click();

        var entry = Assert.Single(nav.History);
        Assert.StartsWith("http://localhost:7071/api/battlenet/login", entry.Uri);
        Assert.Contains("redirect=%2Fruns", entry.Uri);
        Assert.True(entry.Options.ForceLoad);
    }

    [Fact]
    public void LoginPage_Button_Navigates_With_Custom_Redirect()
    {
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/login?redirect=%2Fguild");

        var cut = Render<LoginPage>();

        cut.Find("fluent-button").Click();

        var forceEntry = Assert.Single(nav.History, e => e.Options.ForceLoad);
        Assert.Contains("redirect=%2Fguild", forceEntry.Uri);
    }

    [Fact]
    public void LoginFailedPage_Renders_Title_Subtitle_And_Retry_Button()
    {
        var cut = Render<LoginFailedPage>();

        Assert.Contains(Loc("loginFailed.title"), cut.Markup);
        Assert.Contains(Loc("loginFailed.subtitle"), cut.Markup);
        Assert.Contains(Loc("loginFailed.button"), cut.Markup);
    }

    [Fact]
    public void GoodbyePage_Renders_Title_And_SignIn_Cta()
    {
        var cut = Render<GoodbyePage>();

        Assert.Contains(Loc("goodbye.title"), cut.Markup);
        Assert.Contains(Loc("goodbye.body1"), cut.Markup);
        Assert.Contains(Loc("goodbye.signIn"), cut.Markup);
    }

    [Fact]
    public void PrivacyPolicyPage_Renders_All_Section_Headings()
    {
        var cut = Render<PrivacyPolicyPage>();

        Assert.Contains(Loc("privacy.title"), cut.Markup);
        Assert.Contains(Loc("privacy.controller.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.data.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.cookies.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.thirdParty.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.retention.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.rights.heading"), cut.Markup);
        Assert.Contains(Loc("privacy.contact.heading"), cut.Markup);
    }

    [Fact]
    public void PrivacyPolicyPage_Does_Not_Leak_Email_In_Initial_Markup()
    {
        // Crawl resistance: the contact email must NOT appear in the rendered
        // markup before the user clicks the reveal button. Pin: (1) the
        // PRIVACY_EMAIL substitution pattern is absent, (2) no token looking
        // like a real email literal appears, and (3) the reveal CTA is the
        // entry point a scraper would have to exercise.
        var cut = Render<PrivacyPolicyPage>();

        Assert.DoesNotContain("@dinosauruskeksi", cut.Markup);
        Assert.DoesNotContain("privacy@", cut.Markup);
        Assert.DoesNotContain("security@", cut.Markup);
        // Build-time PRIVACY_EMAIL is never inlined into this page.
        Assert.DoesNotContain("mailto:privacy", cut.Markup);

        // Reveal CTA is present as the gated entry point.
        Assert.Contains(Loc("privacy.contact.reveal"), cut.Markup);

        // The <meta name="robots" content="noindex, nofollow"> tag emitted
        // via <HeadContent> lives in a separate head fragment that bUnit does
        // not include in cut.Markup. The response-layer X-Robots-Tag header is
        // covered by StaticWebAppConfigContractTests. The <meta> duplicate in
        // the page is verified manually in the pre-deploy checklist.
    }
}
