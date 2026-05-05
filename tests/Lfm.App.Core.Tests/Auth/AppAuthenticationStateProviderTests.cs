// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Security.Claims;
using Lfm.App.Auth;
using Lfm.App.i18n;
using Lfm.App.Services;
using Lfm.Contracts.Me;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Auth;

public class AppAuthenticationStateProviderTests
{
    private static MeResponse MakeMe(
        string battleNetId = "player#1234",
        string? guildName = null,
        SelectedCharacterSummaryDto? selectedCharacter = null,
        bool isSiteAdmin = false,
        string? locale = null) =>
        new(
            BattleNetId: battleNetId,
            GuildName: guildName,
            SelectedCharacterId: null,
            SelectedCharacter: selectedCharacter,
            IsSiteAdmin: isSiteAdmin,
            Locale: locale);

    [Fact]
    public async Task GetAuthenticationStateAsync_returns_anonymous_when_me_is_null()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((MeResponse?)null);
        var localeService = new Mock<ILocaleService>();
        var sut = new AppAuthenticationStateProvider(meClient.Object, localeService.Object);

        var state = await sut.GetAuthenticationStateAsync();

        Assert.False(state.User.Identity!.IsAuthenticated);
        Assert.Empty(state.User.Claims);
        localeService.Verify(s => s.SetLocale(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_returns_authenticated_with_battlenet_claims()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe());
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.True(state.User.Identity!.IsAuthenticated);
        Assert.Equal("BattleNet", state.User.Identity.AuthenticationType);
        Assert.Equal("player#1234", state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal("player#1234", state.User.FindFirst(ClaimTypes.Name)!.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_includes_guild_name_claim_when_present()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(guildName: "Stormchasers"));
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.Equal("Stormchasers", state.User.FindFirst("guild_name")!.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_omits_guild_name_claim_when_null_or_empty()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(guildName: null));
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.Null(state.User.FindFirst("guild_name"));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_includes_selected_character_claims_when_present()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(selectedCharacter: new SelectedCharacterSummaryDto(
                Id: "eu-silvermoon-aelrin",
                Name: "Aelrin",
                PortraitUrl: "https://render.worldofwarcraft.com/eu/aelrin-avatar.jpg")));
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        Assert.Equal("eu-silvermoon-aelrin", state.User.FindFirst("selected_character_id")!.Value);
        Assert.Equal("Aelrin", state.User.FindFirst("selected_character_name")!.Value);
        Assert.Equal(
            "https://render.worldofwarcraft.com/eu/aelrin-avatar.jpg",
            state.User.FindFirst("selected_character_portrait_url")!.Value);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_adds_site_admin_role_only_when_flag_is_true()
    {
        var adminClient = new Mock<IMeClient>();
        adminClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(isSiteAdmin: true));
        var memberClient = new Mock<IMeClient>();
        memberClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(isSiteAdmin: false));

        var adminState = await new AppAuthenticationStateProvider(adminClient.Object, Mock.Of<ILocaleService>())
            .GetAuthenticationStateAsync();
        var memberState = await new AppAuthenticationStateProvider(memberClient.Object, Mock.Of<ILocaleService>())
            .GetAuthenticationStateAsync();

        Assert.True(adminState.User.IsInRole("SiteAdmin"));
        Assert.False(memberState.User.IsInRole("SiteAdmin"));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_applies_user_locale_when_set()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(locale: "fi"));
        var localeService = new LocaleService();
        var sut = new AppAuthenticationStateProvider(meClient.Object, localeService);

        await sut.GetAuthenticationStateAsync();

        Assert.Equal("fi", localeService.CurrentLocale);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_does_not_apply_locale_when_null_or_empty()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(locale: null));
        var localeService = new LocaleService();
        var sut = new AppAuthenticationStateProvider(meClient.Object, localeService);

        await sut.GetAuthenticationStateAsync();

        Assert.Equal("en", localeService.CurrentLocale);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_caches_result_across_calls()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MakeMe());
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        await sut.GetAuthenticationStateAsync();
        await sut.GetAuthenticationStateAsync();
        await sut.GetAuthenticationStateAsync();

        meClient.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyStateChanged_clears_cache_so_next_call_refetches()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MakeMe());
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());
        await sut.GetAuthenticationStateAsync();

        sut.NotifyStateChanged();
        await sut.GetAuthenticationStateAsync();

        meClient.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_does_not_cache_anonymous_state_so_next_call_retries()
    {
        // Regression: a transient null from MeClient (cold-start race, brief
        // network blip) used to cement the user as anonymous for the session.
        // The provider must NOT cache the anonymous result; the next call
        // must re-hit MeClient so a recovered backend produces an authenticated
        // state without requiring NotifyStateChanged or a full SPA reload.
        var meClient = new Mock<IMeClient>();
        meClient.SetupSequence(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null)
            .ReturnsAsync(MakeMe());
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var first = await sut.GetAuthenticationStateAsync();
        Assert.False(first.User.Identity!.IsAuthenticated);

        var second = await sut.GetAuthenticationStateAsync();
        Assert.True(second.User.Identity!.IsAuthenticated);

        meClient.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task NotifyStateChanged_fires_AuthenticationStateChanged_event()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(MakeMe());
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());
        await sut.GetAuthenticationStateAsync();
        var eventFired = false;
        sut.AuthenticationStateChanged += _ => { eventFired = true; };

        sut.NotifyStateChanged();

        Assert.True(eventFired);
    }
}
