// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Me;
using Xunit;

namespace Lfm.App.Tests;

public class CharactersPagesTests : ComponentTestBase
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static MeResponse MakeMeResponse(string? selectedCharacterId = null) =>
        new(
            BattleNetId: "player#1234",
            GuildName: null,
            SelectedCharacterId: selectedCharacterId,
            IsSiteAdmin: false,
            Locale: "en");

    private static CharacterDto MakeChar(string name = "Arthas", string realm = "silvermoon") =>
        new(
            Name: name,
            Realm: realm,
            RealmName: "Silvermoon",
            Level: 80,
            Region: "eu",
            ClassId: 1,
            ClassName: "Warrior",
            PortraitUrl: null,
            ActiveSpecId: 71,
            SpecName: "Arms");

    // ── CharactersPage ────────────────────────────────────────────────────────

    [Fact]
    public void CharactersPage_Renders_Loading_Ring_On_Mount()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        var tcs = new TaskCompletionSource<IReadOnlyList<CharacterDto>?>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void CharactersPage_Renders_Character_Cards_After_Load()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar() });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains("Arthas", cut.Markup));
    }

    [Fact]
    public void CharactersPage_Renders_Empty_State_When_No_Characters()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto>());
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains(Loc("characters.empty"), cut.Markup));
    }

    [Fact]
    public void CharactersPage_Renders_Error_State_On_Failure()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains("Network error", cut.Markup));
    }

    [Fact]
    public void CharactersPage_Renders_Error_When_Client_Returns_Null()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CharacterDto>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains(Loc("characters.error.loadFailed"), cut.Markup));
    }

    [Fact]
    public void CharactersPage_Renders_Multiple_Characters()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Arthas", cut.Markup);
            Assert.Contains("Sylvanas", cut.Markup);
        });
    }

    [Fact]
    public void CharactersPage_Renders_Forget_Me_Section()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto>());
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains(Loc("characters.deleteAccount.title"), cut.Markup));
    }

    // ── Active character selection ────────────────────────────────────────────

    [Fact]
    public void CharactersPage_Selected_Card_Shows_Active_Badge_And_Outline()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse(selectedCharacterId: "eu-silvermoon-arthas"));
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            var card = cut.Find("[data-char-id='eu-silvermoon-arthas']");
            Assert.Contains("outline", card.GetAttribute("style") ?? "");
            Assert.Contains(Loc("characters.active"), cut.Markup);
        });
    }

    [Fact]
    public void CharactersPage_Non_Selected_Card_Has_No_Outline()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse(selectedCharacterId: "eu-silvermoon-arthas"));
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            var sylvanasWrapper = cut.Find("[data-char-id='eu-silvermoon-sylvanas']");
            Assert.DoesNotContain("outline", sylvanasWrapper.GetAttribute("style") ?? "");
        });
    }

    [Fact]
    public void CharactersPage_Clicking_Non_Selected_Card_Calls_SelectCharacterAsync()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse(selectedCharacterId: "eu-silvermoon-arthas"));
        me.Setup(m => m.SelectCharacterAsync("eu-silvermoon-sylvanas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find("[data-char-id='eu-silvermoon-sylvanas']"));

        cut.Find("[data-char-id='eu-silvermoon-sylvanas']").Click();

        cut.WaitForAssertion(() =>
            me.Verify(m => m.SelectCharacterAsync("eu-silvermoon-sylvanas", It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void CharactersPage_Clicking_Selected_Card_Does_Not_Call_SelectCharacterAsync()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse(selectedCharacterId: "eu-silvermoon-arthas"));
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find("[data-char-id='eu-silvermoon-arthas']"));

        cut.Find("[data-char-id='eu-silvermoon-arthas']").Click();

        me.Verify(m => m.SelectCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CharactersPage_Select_Failure_Reverts_And_Shows_Error()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse(selectedCharacterId: "eu-silvermoon-arthas"));
        me.Setup(m => m.SelectCharacterAsync("eu-silvermoon-sylvanas", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find("[data-char-id='eu-silvermoon-sylvanas']"));

        cut.Find("[data-char-id='eu-silvermoon-sylvanas']").Click();

        cut.WaitForAssertion(() =>
        {
            // Arthas is still selected (reverted)
            var arthasWrapper = cut.Find("[data-char-id='eu-silvermoon-arthas']");
            Assert.Contains("outline", arthasWrapper.GetAttribute("style") ?? "");
            // Sylvanas has no outline
            var sylvanasWrapper = cut.Find("[data-char-id='eu-silvermoon-sylvanas']");
            Assert.DoesNotContain("outline", sylvanasWrapper.GetAttribute("style") ?? "");
            // Error message shown
            Assert.Contains(Loc("characters.error.selectFailed"), cut.Markup);
        });
    }
}
