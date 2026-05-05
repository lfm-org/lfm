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

    private static GuildDto MakeGuildDto(
        bool isInitialized = true,
        bool requiresSetup = false,
        bool rankDataFresh = true,
        bool canEdit = false,
        string slogan = "We ride the storm",
        string name = "Stormchasers") =>
        new(
            Guild: new GuildInfoDto(1, name, slogan, "Silvermoon", "Alliance",
                120, 10, null, null),
            Setup: new GuildSetupDto(isInitialized, requiresSetup, rankDataFresh, "Europe/Helsinki", "fi"),
            Settings: canEdit
                ? new GuildSettingsDto(
                [
                    new GuildRankPermissionDto(0, true, true, true),
                    new GuildRankPermissionDto(5, false, true, false),
                ])
                : null,
            Editor: new GuildEditorDto(canEdit),
            MemberPermissions: new GuildMemberPermissionsDto(true, true, false));

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
        var dto = MakeGuildDto();
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

    [Fact]
    public void GuildPage_Renders_Setup_Explanation_For_Required_Query()
    {
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(isInitialized: false, requiresSetup: true, canEdit: true));
        Services.AddSingleton(client.Object);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/guild?setup=required");

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guild.setupRequiredExplanation"), cut.Markup));
    }

    [Fact]
    public void GuildPage_Renders_Status_Chips_And_Editor_For_Editable_Guild()
    {
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(isInitialized: false, requiresSetup: true, canEdit: true));
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("guild.chip.settingsPending"), cut.Markup);
            Assert.Contains(Loc("guild.chip.editable"), cut.Markup);
            Assert.Contains(Loc("guild.chip.rankSyncFresh"), cut.Markup);
            Assert.Contains(Loc("guild.chip.members", 120), cut.Markup);
            Assert.Contains(Loc("guild.chip.ranksDetected", 10), cut.Markup);
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup);
        });
    }

    [Fact]
    public void GuildPage_Renders_Stale_RankSync_Warning_When_RankData_NotFresh()
    {
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(rankDataFresh: false, canEdit: true));
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("guild.chip.rankSyncStale"), cut.Markup);
            Assert.Contains(Loc("guild.rankSyncStale"), cut.Markup);
        });
    }

    [Fact]
    public void GuildPage_Save_Sends_Draft_And_Resets_Unsaved_Baseline()
    {
        var initial = MakeGuildDto(canEdit: true, slogan: "Old slogan");
        var updated = MakeGuildDto(canEdit: true, slogan: "New slogan");
        UpdateGuildRequest? captured = null;
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);
        client.Setup(c => c.UpdateAsync(It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateGuildRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(updated);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup));
        cut.Find("#guild-slogan").Change("New slogan");

        var saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.settings.save"), StringComparison.Ordinal));
        saveButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.UpdateAsync(
                It.IsAny<UpdateGuildRequest>(),
                It.IsAny<CancellationToken>()),
                Times.Once));
        Assert.NotNull(captured);
        Assert.Equal("Europe/Helsinki", captured!.Timezone);
        Assert.Equal("fi", captured.Locale);
        Assert.Equal("New slogan", captured.Slogan);
        Assert.Equal(2, captured.RankPermissions?.Count);

        nav.NavigateTo("/runs");

        cut.WaitForAssertion(() =>
            Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath));
        Assert.DoesNotContain(Loc("unsavedChanges.title"), cut.Markup);
    }

    [Fact]
    public void GuildPage_Setup_Query_Explanation_Clears_After_Initial_Setup_Save()
    {
        var initial = MakeGuildDto(
            isInitialized: false,
            requiresSetup: true,
            canEdit: true,
            slogan: "Old slogan");
        var updated = MakeGuildDto(
            isInitialized: true,
            requiresSetup: false,
            canEdit: true,
            slogan: "Ready slogan");
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);
        client.Setup(c => c.UpdateAsync(It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/guild?setup=required");

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guild.setupRequiredExplanation"), cut.Markup));

        var saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.settings.save"), StringComparison.Ordinal));
        saveButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.UpdateAsync(
                It.IsAny<UpdateGuildRequest>(),
                It.IsAny<CancellationToken>()),
                Times.Once));
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(Loc("guild.setupRequiredExplanation"), cut.Markup));
    }

    // ── GuildAdminPage ───────────────────────────────────────────────────────

    [Fact]
    public void GuildAdminPage_Shows_Title()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var client = new Mock<IGuildClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        Assert.Contains(Loc("guildAdmin.title"), cut.Markup);
    }

    [Fact]
    public void GuildAdminPage_NonSiteAdmin_DoesNotRenderAdminEditorControls()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var client = new Mock<IGuildClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        Assert.DoesNotContain(Loc("guildAdmin.loadButton"), cut.Markup);
        Assert.DoesNotContain(Loc("guildAdmin.settings.save"), cut.Markup);
        client.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.GetAdminAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.UpdateAsync(It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        client.Verify(c => c.UpdateAdminAsync(It.IsAny<string>(), It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GuildAdminPage_SiteAdmin_Loads_Guild_By_Entered_GuildId()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var dto = MakeGuildDto(canEdit: true);
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        var loadButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal));
        loadButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>()), Times.Once));
        Assert.Contains("Stormchasers", cut.Markup);
    }

    [Fact]
    public void GuildAdminPage_Save_Uses_UpdateAdminAsync_For_Loaded_GuildId()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var initial = MakeGuildDto(canEdit: true, slogan: "Old slogan");
        var updated = MakeGuildDto(canEdit: true, slogan: "New slogan");
        UpdateGuildRequest? captured = null;
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);
        client.Setup(c => c.UpdateAdminAsync("99", It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, UpdateGuildRequest, CancellationToken>((_, request, _) => captured = request)
            .ReturnsAsync(updated);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup));
        cut.Find("#guild-slogan").Change("New slogan");

        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.settings.save"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.UpdateAdminAsync(
                "99",
                It.IsAny<UpdateGuildRequest>(),
                It.IsAny<CancellationToken>()),
                Times.Once));
        client.Verify(c => c.UpdateAsync(It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.NotNull(captured);
        Assert.Equal("New slogan", captured!.Slogan);
    }

    [Fact]
    public void GuildAdminPage_Renders_Identity_Id_Rank_And_Member_Chips_After_Load()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(canEdit: true));
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Stormchasers", cut.Markup);
            Assert.Contains(Loc("guildAdmin.guildIdChip", "99"), cut.Markup);
            Assert.Contains(Loc("guild.chip.rankSyncFresh"), cut.Markup);
            Assert.Contains(Loc("guild.chip.members", 120), cut.Markup);
            Assert.Contains(Loc("guild.chip.ranksDetected", 10), cut.Markup);
        });
    }

    [Fact]
    public void GuildAdminPage_DirtySettings_ConfirmsBeforeLoadingDifferentGuild()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var initial = MakeGuildDto(canEdit: true, slogan: "Old slogan");
        var other = MakeGuildDto(canEdit: true, slogan: "Other slogan", name: "Nightwatch");
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);
        client.Setup(c => c.GetAdminAsync("100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(other);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup));
        cut.Find("#guild-slogan").Change("Changed slogan");

        cut.Find("#guild-admin-guild-id").Change("100");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("unsavedChanges.title"), cut.Markup));
        client.Verify(c => c.GetAdminAsync("100", It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("Stormchasers", cut.Markup);

        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("unsavedChanges.leave"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.GetAdminAsync("100", It.IsAny<CancellationToken>()), Times.Once));
        Assert.Contains("Nightwatch", cut.Markup);
    }

    [Fact]
    public void GuildAdminPage_DisablesLoadControls_WhileSaving()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
        var initial = MakeGuildDto(canEdit: true, slogan: "Old slogan");
        var saveTcs = new TaskCompletionSource<GuildDto?>();
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>()))
            .ReturnsAsync(initial);
        client.Setup(c => c.UpdateAdminAsync("99", It.IsAny<UpdateGuildRequest>(), It.IsAny<CancellationToken>()))
            .Returns(saveTcs.Task);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("guildAdmin.settings.save"), cut.Markup));
        cut.Find("#guild-slogan").Change("New slogan");

        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.settings.save"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
        {
            var loadButton = cut.FindAll("fluent-button")
                .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal));
            Assert.True(loadButton.HasAttribute("disabled"));
            Assert.True(cut.Find("#guild-admin-guild-id").HasAttribute("disabled"));
        });
    }

    [Fact]
    public void GuildAdminPage_DirtySettings_BlocksInternalNavigation_AndLeaveContinues()
    {
        this.AddAuthorization().SetAuthorized("admin#1").SetRoles("SiteAdmin");
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
        client.Setup(c => c.GetAdminAsync("99", It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<GuildAdminPage>();

        cut.Find("#guild-admin-guild-id").Change("99");
        cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("guildAdmin.loadButton"), StringComparison.Ordinal))
            .Click();
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
