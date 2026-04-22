// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Admin;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Lfm.App.Tests;

/// <summary>
/// Coverage for the /admin/reference page: SiteAdmin role gate + button
/// wiring + result rendering + error surfacing.
/// </summary>
public class AdminReferenceRefreshPageTests : ComponentTestBase
{
    [Fact]
    public void NonAdmin_sees_the_NotAuthorized_fallback_and_the_refresh_button_is_not_rendered()
    {
        // A signed-in user without the SiteAdmin role must not see the refresh
        // button. Blazor's <AuthorizeView>/[Authorize] uses NotAuthorized
        // markup from AuthorizeRouteView, which bunit renders as the default
        // "Not authorized" message.
        this.AddAuthorization().SetAuthorized("player#1234");
        var client = new Mock<IWowReferenceAdminClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<AdminReferenceRefreshPage>();

        // The refresh button label isn't in the markup when the role gate fails.
        Assert.DoesNotContain(Loc("adminReference.refreshButton"), cut.Markup);
        client.Verify(c => c.RefreshAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void SiteAdmin_sees_the_title_description_and_refresh_button()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var client = new Mock<IWowReferenceAdminClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<AdminReferenceRefreshPage>();

        Assert.Contains(Loc("adminReference.title"), cut.Markup);
        Assert.Contains(Loc("adminReference.description"), cut.Markup);
        Assert.Contains(Loc("adminReference.refreshButton"), cut.Markup);
    }

    [Fact]
    public void Clicking_Refresh_invokes_the_client_and_renders_the_results_table()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var tcs = new TaskCompletionSource<WowReferenceRefreshResponse>();
        tcs.SetResult(new WowReferenceRefreshResponse(
        [
            new WowReferenceRefreshEntityResult("instances", "synced (12 docs)"),
            new WowReferenceRefreshEntityResult("specializations", "synced (36 docs)"),
            new WowReferenceRefreshEntityResult("expansions", "synced (10 docs)"),
        ]));
        var client = new Mock<IWowReferenceAdminClient>();
        client.Setup(c => c.RefreshAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        Services.AddSingleton(client.Object);

        var cut = Render<AdminReferenceRefreshPage>();
        cut.Find("fluent-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("synced (12 docs)", cut.Markup);
            Assert.Contains("specializations", cut.Markup);
            Assert.Contains("expansions", cut.Markup);
        });
        client.Verify(c => c.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Failed_entity_status_carries_data_status_failed_CSS_hook()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var client = new Mock<IWowReferenceAdminClient>();
        client.Setup(c => c.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WowReferenceRefreshResponse(
            [
                new WowReferenceRefreshEntityResult("instances", "failed: Blizzard returned 503"),
                new WowReferenceRefreshEntityResult("expansions", "synced (10 docs)"),
            ]));
        Services.AddSingleton(client.Object);

        var cut = Render<AdminReferenceRefreshPage>();
        cut.Find("fluent-button").Click();

        cut.WaitForAssertion(() =>
        {
            var rows = cut.FindAll("tr[data-status]");
            // The header row has no data-status; only body rows do.
            Assert.Contains(rows, r => r.GetAttribute("data-status") == "failed");
            Assert.Contains(rows, r => r.GetAttribute("data-status") == "ok");
        });
    }

    [Fact]
    public void Client_exception_surfaces_as_an_inline_error_not_a_toast()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var client = new Mock<IWowReferenceAdminClient>();
        client.Setup(c => c.RefreshAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("connection refused"));
        Services.AddSingleton(client.Object);

        var cut = Render<AdminReferenceRefreshPage>();
        cut.Find("fluent-button").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("connection refused", cut.Markup);
            Assert.Contains(Loc("adminReference.errorPrefix"), cut.Markup);
        });
    }
}
