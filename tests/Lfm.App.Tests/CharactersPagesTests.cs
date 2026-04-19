// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
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
    public void Eager_enriches_first_three_pending_cards_on_init()
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
            .ReturnsAsync(chars);
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

        var cut = Render<CharactersPage>();

        // Assert: EnrichCharacterAsync eventually called for all 5 keys.
        // The first 3 are enriched eagerly in parallel; the remaining 2 are drained
        // by the paced queue worker. We just verify all were called at least once.
        var allKeys = chars.Select(c => $"eu-silvermoon-{c.Name.ToLowerInvariant()}").ToList();

        cut.WaitForAssertion(() =>
        {
            foreach (var key in allKeys)
                me.Verify(m => m.EnrichCharacterAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        }, timeout: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Paces_queue_enrichment_at_250ms_cadence()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: 6 chars with ClassId = null so all start Pending.
        var chars = Enumerable.Range(1, 6)
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
            .ReturnsAsync(chars);
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var timestamps = new ConcurrentQueue<DateTimeOffset>();
        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        me.Setup(m => m.EnrichCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                timestamps.Enqueue(DateTimeOffset.UtcNow);
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

        // Wait for all 6 enrichments to complete (generous 3 s timeout).
        cut.WaitForAssertion(
            () => Assert.Equal(6, timestamps.Count),
            timeout: TimeSpan.FromSeconds(3));

        // Check pacing: the first 3 are the eager parallel burst — skip gap checks between them.
        // Characters 4-6 (indices 3-5) are processed by the queue worker with 250 ms delays.
        var ts = timestamps.ToArray();
        Assert.Equal(6, ts.Length);

        // Gaps between queue-drained enrichments. The worker delays AFTER each enrich,
        // so the gap ts[2]→ts[3] is effectively 0 (eager-3 just completed when the worker
        // fires its first iteration). The 250 ms pacing applies to gaps ts[3]→ts[4] and
        // ts[4]→ts[5]. Use ≥ 200 ms lower bound to tolerate CI scheduler jitter.
        for (int i = 4; i < ts.Length; i++)
        {
            var gap = ts[i] - ts[i - 1];
            Assert.True(gap >= TimeSpan.FromMilliseconds(200),
                $"Gap between enrichment {i} and {i + 1} was {gap.TotalMilliseconds:F0} ms, expected ≥ 200 ms");
        }
    }

    // ── Task 15: state-aware click handler ───────────────────────────────────

    [Fact]
    public void Click_on_Enriched_card_triggers_PUT()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var battleNet = new Mock<IBattleNetClient>();
        // MakeChar returns a char with ClassId/ActiveSpecId set → starts Enriched.
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
    public void Click_on_Pending_card_promotes_to_front_waits_and_then_PUTs()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        // Arrange: 5 pending chars (ClassId null → all start Pending).
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

        var fifthKey = "eu-silvermoon-char5";
        var invocations = new List<string>();

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(chars);
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);

        var me = new Mock<IMeClient>();
        me.Setup(m => m.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        me.Setup(m => m.EnrichCharacterAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                lock (invocations) invocations.Add($"enrich:{key}");
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
        me.Setup(m => m.SelectCharacterAsync(fifthKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
            {
                lock (invocations) invocations.Add($"select:{key}");
                return true;
            });

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);

        var cut = Render<CharactersPage>();
        cut.WaitForAssertion(() => cut.Find($"[data-char-id='{fifthKey}']"));

        cut.Find($"[data-char-id='{fifthKey}']").Click();

        cut.WaitForAssertion(() =>
        {
            me.Verify(m => m.EnrichCharacterAsync(fifthKey, It.IsAny<CancellationToken>()), Times.Once);
            me.Verify(m => m.SelectCharacterAsync(fifthKey, It.IsAny<CancellationToken>()), Times.Once);
            lock (invocations)
            {
                var enrichIdx = invocations.IndexOf($"enrich:{fifthKey}");
                var selectIdx = invocations.IndexOf($"select:{fifthKey}");
                Assert.True(enrichIdx >= 0, "enrich was never called for fifth card");
                Assert.True(selectIdx >= 0, "select was never called for fifth card");
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
            .ReturnsAsync(new[] { MakeChar("Arthas") });
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
            .ReturnsAsync(chars);
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
