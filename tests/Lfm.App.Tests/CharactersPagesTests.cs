using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Xunit;

namespace Lfm.App.Tests;

public class CharactersPagesTests : ComponentTestBase
{
    // ── helpers ──────────────────────────────────────────────────────────────

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

        cut.FindAll("fluent-progress-ring").Should().NotBeEmpty();
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Arthas"));
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Loc("characters.empty")));
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Network error"));
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Loc("characters.error.loadFailed")));
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
            cut.Markup.Should().Contain("Arthas");
            cut.Markup.Should().Contain("Sylvanas");
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

        cut.WaitForAssertion(() => cut.Markup.Should().Contain(Loc("characters.deleteAccount.title")));
    }
}
