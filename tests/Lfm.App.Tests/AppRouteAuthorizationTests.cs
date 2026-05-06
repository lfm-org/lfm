// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Lfm.App;
using Lfm.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Lfm.App.Tests;

public class AppRouteAuthorizationTests : ComponentTestBase
{
    [Fact]
    public void SiteAdminRoute_Anonymous_RedirectsToLogin()
    {
        this.AddAuthorization();
        Services.AddSingleton(new Mock<IWowReferenceAdminClient>().Object);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/admin/reference");

        Render<App>();

        Assert.Equal("/login?redirect=%2Fadmin%2Freference", new Uri(nav.Uri).PathAndQuery);
    }

    [Fact]
    public void SiteAdminRoute_AuthenticatedNonAdmin_ShowsAccessDeniedInsteadOfRedirectingToLogin()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        Services.AddSingleton(new Mock<IWowReferenceAdminClient>().Object);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/admin/reference");

        var cut = Render<App>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("/admin/reference", new Uri(nav.Uri).AbsolutePath);
            Assert.Contains(Loc("auth.notAuthorized.title"), cut.Markup);
            Assert.Contains(Loc("auth.notAuthorized.body"), cut.Markup);
            Assert.NotEmpty(cut.FindAll("[data-testid='route-access-denied']"));
        });
    }
}
