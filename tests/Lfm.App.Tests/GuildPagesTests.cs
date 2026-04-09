using Bunit;
using FluentAssertions;
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

        cut.FindAll("fluent-progress-ring").Should().NotBeEmpty();
    }

    [Fact]
    public void GuildPage_Renders_No_Guild_Message_When_Guild_Is_Null()
    {
        var client = new Mock<IGuildClient>();
        var dto = new GuildDto(
            Guild: null,
            Setup: new GuildSetupDto(false, true, false, null, "Europe/Helsinki", "fi"),
            Settings: null,
            Editor: new GuildEditorDto(false, "member"),
            MemberPermissions: new GuildMemberPermissionsDto(null, false, false, false, false));
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain(Loc("guild.noGuild.title")));

        client.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GuildPage_Renders_Guild_Name_After_Load()
    {
        var client = new Mock<IGuildClient>();
        var dto = new GuildDto(
            Guild: new GuildInfoDto(1, "Stormchasers", "We ride the storm", "silvermoon", "Silvermoon", "Alliance",
                120, 5000, 100, 10, null, null),
            Setup: new GuildSetupDto(true, false, true, null, "Europe/Helsinki", "fi"),
            Settings: null,
            Editor: new GuildEditorDto(false, "member"),
            MemberPermissions: new GuildMemberPermissionsDto(3, true, true, false, true));
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Stormchasers"));

        client.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GuildPage_Renders_Error_When_Client_Returns_Null()
    {
        var client = new Mock<IGuildClient>();
        client.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((GuildDto?)null);
        Services.AddSingleton(client.Object);

        var cut = Render<GuildPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Failed to load guild data."));

        client.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── GuildAdminPage ───────────────────────────────────────────────────────

    [Fact]
    public void GuildAdminPage_Renders_Without_Crash()
    {
        var client = new Mock<IGuildClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        cut.Markup.Should().NotBeEmpty();
    }

    [Fact]
    public void GuildAdminPage_Shows_Title()
    {
        var client = new Mock<IGuildClient>();
        Services.AddSingleton(client.Object);

        var cut = Render<GuildAdminPage>();

        cut.Markup.Should().Contain(Loc("guildAdmin.title"));
    }
}
