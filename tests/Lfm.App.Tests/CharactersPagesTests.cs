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

    private (Mock<IBattleNetClient> BattleNet, Mock<IMeClient> Me) RegisterCharactersPageClients(
        IReadOnlyList<CharacterDto> chars,
        MeResponse? meResponse = null)
    {
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(chars));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(meResponse);

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);
        return (battleNet, me);
    }

    // ── CharactersPage ────────────────────────────────────────────────────────

    [Fact]
    public void CharactersPage_Renders_Loading_Ring_On_Mount()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        var tcs = new TaskCompletionSource<CharactersFetchResult>();
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar() }));
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto>()));
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
    public void CharactersPage_Renders_Error_When_Client_Returns_Error()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Error());
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() => Assert.Contains(Loc("characters.error.loadFailed"), cut.Markup));
    }

    [Fact]
    public void CharactersPage_Auto_Refreshes_When_Client_Returns_NeedsRefresh()
    {
        // Regression guard for bug: first visit to /characters used to show
        // "load failed" until the user clicked refresh.  Backend returns 204
        // (NeedsRefresh) when no cached account profile exists; the page must
        // auto-POST to /refresh in that case.
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.NeedsRefresh());
        battleNet.Setup(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Arthas") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            battleNet.Verify(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Arthas", cut.Markup);
        });
    }

    [Fact]
    public void CharactersPage_RefreshButton_Click_Replaces_Character_List()
    {
        // Proves the "Refresh from Battle.net" button is wired to
        // IBattleNetClient.RefreshCharactersAsync and that its returned
        // list replaces the currently-rendered cards. This used to be an
        // E2E test that asserted only an outbound HTTP request was fired
        // (E-HC-F10); the integration-layer contract is covered by
        // BattleNetCharactersRefreshFunctionTests, and the user-observable
        // re-render is bUnit territory.
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas") }));
        battleNet.Setup(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeChar("Sylvanas", "silvermoon") });
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => Assert.Contains("Arthas", cut.Markup));

        var refreshButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("characters.refresh")));
        refreshButton.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Sylvanas", cut.Markup);
            Assert.DoesNotContain("Arthas", cut.Markup);
        });
    }

    [Fact]
    public void CharactersPage_Renders_Multiple_Characters()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") }));
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
    public void CharactersPage_Sorts_By_Name_When_Query_Sort_Name()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        RegisterCharactersPageClients(new List<CharacterDto>
        {
            MakeChar("Zed"),
            MakeChar("Aelrin"),
        });
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/characters?sort=name");

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            var aelrinIndex = cut.Markup.IndexOf("Aelrin", StringComparison.Ordinal);
            var zedIndex = cut.Markup.IndexOf("Zed", StringComparison.Ordinal);

            Assert.True(aelrinIndex >= 0, "Aelrin should be rendered.");
            Assert.True(zedIndex >= 0, "Zed should be rendered.");
            Assert.True(aelrinIndex < zedIndex, $"Expected Aelrin before Zed. Markup: {cut.Markup}");
        });
    }

    [Fact]
    public void CharactersPage_Paginates_Characters_And_Updates_Query()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        RegisterCharactersPageClients(Enumerable.Range(1, 4)
            .Select(i => MakeChar($"Char{i}"))
            .ToList());
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/characters");

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Char1", cut.Markup);
            Assert.DoesNotContain("Char4", cut.Markup);
        });

        var nextButton = Assert.Single(
            cut.FindAll("fluent-button"),
            b => b.GetAttribute("aria-label") == Loc("common.next"));
        nextButton.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("page=2", nav.Uri);
            Assert.Contains("Char4", cut.Markup);
            Assert.DoesNotContain("Char1", cut.Markup);
        });

        cut.Find("#characters-sort").Change("name");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("sort=name", nav.Uri);
            Assert.DoesNotContain("page=", nav.Uri);
        });
    }

    [Fact]
    public void CharactersPage_Navigates_To_Safe_Redirect_After_Select()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var (_, me) = RegisterCharactersPageClients(
            new List<CharacterDto> { MakeChar("Aelrin") },
            MakeMeResponse());
        me.Setup(m => m.SelectCharacterAsync("eu-silvermoon-aelrin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/characters?redirect=%2Fruns%2Fnew");

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find("[data-char-id='eu-silvermoon-aelrin']"));

        cut.Find("[data-char-id='eu-silvermoon-aelrin']").Click();

        cut.WaitForAssertion(() =>
        {
            me.Verify(m => m.SelectCharacterAsync("eu-silvermoon-aelrin", It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(nav.Uri.EndsWith("/runs/new", StringComparison.Ordinal), nav.Uri);
        });
    }

    [Theory]
    [InlineData("https%3A%2F%2Fevil.test")]
    [InlineData("%2F%2Fevil.test")]
    public void CharactersPage_Falls_Back_To_Runs_For_Unsafe_Redirect(string encodedRedirect)
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var (_, me) = RegisterCharactersPageClients(
            new List<CharacterDto> { MakeChar("Aelrin") },
            MakeMeResponse());
        me.Setup(m => m.SelectCharacterAsync("eu-silvermoon-aelrin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo($"/characters?redirect={encodedRedirect}");

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find("[data-char-id='eu-silvermoon-aelrin']"));

        cut.Find("[data-char-id='eu-silvermoon-aelrin']").Click();

        cut.WaitForAssertion(() =>
        {
            me.Verify(m => m.SelectCharacterAsync("eu-silvermoon-aelrin", It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(nav.Uri.EndsWith("/runs", StringComparison.Ordinal), nav.Uri);
            Assert.DoesNotContain("evil.test", nav.Uri);
        });
    }

    [Fact]
    public void CharactersPage_Renders_Forget_Me_Section()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto>()));
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas") }));
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
    public void CharactersPage_Selected_Badge_Renders_When_Server_Id_Differs_In_Case()
    {
        // Regression: server normalises region/realm/name to lowercase before
        // storing me.SelectedCharacterId (see CharacterPortraitService.TryNormalise).
        // CharKey used to be `$"{c.Region}-{c.Realm}-{c.Name.ToLowerInvariant()}"`,
        // which for a CharacterDto whose Region/Realm came back from Blizzard in
        // mixed case would never equal the lowercase server value, silently
        // hiding the active-character badge across page navigations.
        this.AddAuthorization().SetAuthorized("player#1234");
        var mixedCase = new CharacterDto(
            Name: "Arthas", Realm: "Silvermoon", RealmName: "Silvermoon", Level: 80,
            Region: "EU", ClassId: 1, ClassName: "Warrior", PortraitUrl: null,
            ActiveSpecId: 71, SpecName: "Arms");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { mixedCase }));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        // Server-stored SelectedCharacterId is fully lowercase.
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") }));
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") }));
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
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas") }));
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
    public async Task Eager_enriches_first_visible_page_only_on_init()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: 5 chars with ClassId = null so all start Pending.
        var chars = Enumerable.Range(1, 5)
            .Select(i => new CharacterDto(
                Name: $"Char{i}",
                Realm: "silvermoon",
                RealmName: "Silvermoon",
                Level: 80,
                Region: "eu",
                ClassId: null,
                ClassName: null,
                PortraitUrl: null,
                ActiveSpecId: null,
                SpecName: null))
            .ToList();

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(chars));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        me.Setup(m => m.EnrichCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => new CharacterDto(
                Name: key.Split('-')[2],
                Realm: "silvermoon",
                RealmName: "Silvermoon",
                Level: 80,
                Region: "eu",
                ClassId: 1,
                ClassName: "Warrior",
                PortraitUrl: null,
                ActiveSpecId: 71,
                SpecName: "Arms"));

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        Render<CharactersPage>();

        await Task.Delay(TimeSpan.FromMilliseconds(350));
        for (var i = 1; i <= 3; i++)
        {
            me.Verify(m => m.EnrichCharacterAsync($"eu-silvermoon-char{i}", It.IsAny<CancellationToken>()), Times.Once);
        }

        me.Verify(m => m.EnrichCharacterAsync("eu-silvermoon-char4", It.IsAny<CancellationToken>()), Times.Never);
        me.Verify(m => m.EnrichCharacterAsync("eu-silvermoon-char5", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void CharactersPage_Does_Not_Render_Non_Visible_Page_Cards()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: 6 chars with ClassId = null so all start Pending.
        var chars = Enumerable.Range(1, 4)
            .Select(i => new CharacterDto(
                Name: $"Char{i}",
                Realm: "silvermoon",
                RealmName: "Silvermoon",
                Level: 80,
                Region: "eu",
                ClassId: null,
                ClassName: null,
                PortraitUrl: null,
                ActiveSpecId: null,
                SpecName: null))
            .ToList();

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(chars));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        me.Setup(m => m.EnrichCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                return new CharacterDto(
                    Name: key.Split('-')[2],
                    Realm: "silvermoon",
                    RealmName: "Silvermoon",
                    Level: 80,
                    Region: "eu",
                    ClassId: 1,
                    ClassName: "Warrior",
                    PortraitUrl: null,
                    ActiveSpecId: 71,
                    SpecName: "Arms");
            });

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-char-id='eu-silvermoon-char1']");
            cut.Find("[data-char-id='eu-silvermoon-char3']");
            Assert.Empty(cut.FindAll("[data-char-id='eu-silvermoon-char4']"));
        });
    }

    // ── Task 15: state-aware click handler ───────────────────────────────────

    [Fact]
    public void Click_on_Enriched_card_triggers_PUT()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        // MakeChar returns a char with ClassId/ActiveSpecId set → starts Enriched.
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") }));
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
    public async Task Click_on_Pending_visible_card_waits_for_enrich_then_PUTs()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: 1 pending char (ClassId null → starts Pending).
        var chars = Enumerable.Range(1, 1)
            .Select(i => new CharacterDto(
                Name: $"Char{i}",
                Realm: "silvermoon",
                RealmName: "Silvermoon",
                Level: 80,
                Region: "eu",
                ClassId: null,
                ClassName: null,
                PortraitUrl: null,
                ActiveSpecId: null,
                SpecName: null))
            .ToList();

        var key = "eu-silvermoon-char1";
        var invocations = new List<string>();
        var enrichStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseEnrich = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(chars));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        me.Setup(m => m.EnrichCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string characterKey, CancellationToken _) =>
            {
                lock (invocations) invocations.Add($"enrich:{characterKey}");
                enrichStarted.SetResult();
                await releaseEnrich.Task;
                return new CharacterDto(
                    Name: characterKey.Split('-')[2],
                    Realm: "silvermoon",
                    RealmName: "Silvermoon",
                    Level: 80,
                    Region: "eu",
                    ClassId: 1,
                    ClassName: "Warrior",
                    PortraitUrl: null,
                    ActiveSpecId: 71,
                    SpecName: "Arms");
            });
        me.Setup(m => m.SelectCharacterAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string characterKey, CancellationToken _) =>
            {
                lock (invocations) invocations.Add($"select:{characterKey}");
                return true;
            });

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find($"[data-char-id='{key}']"));
        await enrichStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cut.Find($"[data-char-id='{key}']").Click();
        me.Verify(m => m.SelectCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Never);
        releaseEnrich.SetResult();

        cut.WaitForAssertion(() =>
        {
            me.Verify(m => m.EnrichCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Once);
            me.Verify(m => m.SelectCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Once);
            lock (invocations)
            {
                var enrichIdx = invocations.IndexOf($"enrich:{key}");
                var selectIdx = invocations.IndexOf($"select:{key}");
                Assert.True(enrichIdx >= 0, "enrich was never called for visible card");
                Assert.True(selectIdx >= 0, "select was never called for visible card");
                Assert.True(enrichIdx < selectIdx, $"enrich (idx {enrichIdx}) must precede select (idx {selectIdx})");
            }
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CharactersPage_Character_Card_Is_A_Button()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new[] { MakeChar("Arthas") }));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMeResponse());
        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        cut.WaitForAssertion(() =>
        {
            var cards = cut.FindAll("button.character-card");
            Assert.NotEmpty(cards);
        });
    }

    [Fact]
    public void Click_on_Failed_card_retries_enrich_then_PUTs()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: one pending char (ClassId null → starts Pending).
        // First EnrichCharacterAsync call returns null (fail); second returns a valid DTO.
        var chars = new List<CharacterDto>
        {
            new(
                Name: "Arthas",
                Realm: "silvermoon",
                RealmName: "Silvermoon",
                Level: 80,
                Region: "eu",
                ClassId: null,
                ClassName: null,
                PortraitUrl: null,
                ActiveSpecId: null,
                SpecName: null)
        };
        var key = "eu-silvermoon-arthas";

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(chars));
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var enrichedDto = new CharacterDto(
            Name: "Arthas",
            Realm: "silvermoon",
            RealmName: "Silvermoon",
            Level: 80,
            Region: "eu",
            ClassId: 1,
            ClassName: "Warrior",
            PortraitUrl: null,
            ActiveSpecId: 71,
            SpecName: "Arms");

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        // First call fails (returns null); second call succeeds.
        me.SetupSequence(m => m.EnrichCharacterAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CharacterDto?)null)
            .ReturnsAsync(enrichedDto);
        me.Setup(m => m.SelectCharacterAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();

        // Wait for eager enrich to complete (first call → null → Failed state).
        cut.WaitForAssertion(() =>
            me.Verify(m => m.EnrichCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Once),
            timeout: TimeSpan.FromSeconds(3));

        // Now click — card is Failed, so HandleSelectCharacter calls EnrichAsync directly (second call).
        cut.Find($"[data-char-id='{key}']").Click();

        cut.WaitForAssertion(() =>
        {
            me.Verify(m => m.EnrichCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Exactly(2));
            me.Verify(m => m.SelectCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        }, timeout: TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void CharactersPage_Select_Failure_Reverts_And_Shows_Error()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeChar("Arthas"), MakeChar("Sylvanas") }));
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
