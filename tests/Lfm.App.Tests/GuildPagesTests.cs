// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Guild;
using Xunit;

namespace Lfm.App.Tests;

public class GuildPagesTests : ComponentTestBase
{
    // ── GuildPage ────────────────────────────────────────────────────────────

    [Fact]
    public void GuildPage_Renders_Loading_Ring_On_Mount()
    {
        var client = new Mock<IGuildClient>();
        var tcs = new TaskCompletionSource<GuildDto?>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void GuildPage_Renders_No_Guild_Message_When_Guild_Is_Null()
    {
        var client = new Mock<IGuildClient>();
        var dto = new GuildDto(
            Guild: null,
            Setup: new GuildSetupDto(false, true, false, "Europe/Helsinki", "fi"),
            Settings: null,
            Editor: new GuildEditorDto(false),
            MemberPermissions: new GuildMemberPermissionsDto(false, false, false));
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guild.noGuild.title"), cut.Markup));
    }

    [Fact]
    public void GuildPage_Renders_Guild_Name_After_Load()
    {
        var client = new Mock<IGuildClient>();
        var dto = new GuildDto(
            Guild: new GuildInfoDto(1, "Stormchasers", "We ride the storm", "Silvermoon", "Alliance",
                120, 10, null, null),
            Setup: new GuildSetupDto(true, false, true, "Europe/Helsinki", "fi"),
            Settings: null,
            Editor: new GuildEditorDto(false),
            MemberPermissions: new GuildMemberPermissionsDto(true, true, false));
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains("Stormchasers", cut.Markup));
    }

    [Fact]
    public void GuildPage_Renders_Error_When_Client_Returns_Null()
    {
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GuildDto?)null);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guild.error.loadFailed"), cut.Markup));
    }

    // ── GuildAdminPage ───────────────────────────────────────────────────────

    [Fact]
    public void GuildAdminPage_Shows_Title()
    {
        var client = new Mock<IGuildClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        Assert.Contains(Loc("guildAdmin.title"), cut.Markup);
    }

    [Fact]
    public void GuildAdminPage_DirtySettings_BlocksInternalNavigation_AndLeaveContinues()
    {
        var dto = new GuildDto(
            Guild: new GuildInfoDto(1, "Stormchasers", "Old slogan", "Silvermoon", "Alliance",
                120, 10, null, null),
            Setup: new GuildSetupDto(true, false, true, "Europe/Helsinki", "fi"),
            Settings: new GuildSettingsDto(
            [
                new GuildRankPermissionDto(0, true, true, true),
            ]),
            Editor: new GuildEditorDto(true),
            MemberPermissions: new GuildMemberPermissionsDto(true, true, true));

        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<GuildAdminPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup));
        cut.Find("#guild-slogan").Change("New slogan");

        nav.NavigateTo("/runs");

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("unsavedChanges.title"), cut.Markup));
        Assert.Equal(NavigationState.Prevented, nav.History.First().State);

        var leaveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("unsavedChanges.leave"), StringComparison.Ordinal));
        leaveButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath));
        Assert.Equal(NavigationState.Succeeded, nav.History.First().State);
    }

}
