using System.Security.Claims;
using FluentAssertions;
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
        bool isSiteAdmin = false,
        string? locale = null) =>
        new(
            BattleNetId: battleNetId,
            GuildName: guildName,
            SelectedCharacterId: null,
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

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
        state.User.Claims.Should().BeEmpty();
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

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.Identity.AuthenticationType.Should().Be("BattleNet");
        state.User.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be("player#1234");
        state.User.FindFirst(ClaimTypes.Name)!.Value.Should().Be("player#1234");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_includes_guild_name_claim_when_present()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(guildName: "Stormchasers"));
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        state.User.FindFirst("guild_name")!.Value.Should().Be("Stormchasers");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_omits_guild_name_claim_when_null_or_empty()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(guildName: null));
        var sut = new AppAuthenticationStateProvider(meClient.Object, Mock.Of<ILocaleService>());

        var state = await sut.GetAuthenticationStateAsync();

        state.User.FindFirst("guild_name").Should().BeNull();
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

        adminState.User.IsInRole("SiteAdmin").Should().BeTrue();
        memberState.User.IsInRole("SiteAdmin").Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_applies_user_locale_when_set()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(locale: "fi"));
        var localeService = new Mock<ILocaleService>();
        var sut = new AppAuthenticationStateProvider(meClient.Object, localeService.Object);

        await sut.GetAuthenticationStateAsync();

        localeService.Verify(s => s.SetLocale("fi"), Times.Once);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_does_not_apply_locale_when_null_or_empty()
    {
        var meClient = new Mock<IMeClient>();
        meClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeMe(locale: null));
        var localeService = new Mock<ILocaleService>();
        var sut = new AppAuthenticationStateProvider(meClient.Object, localeService.Object);

        await sut.GetAuthenticationStateAsync();

        localeService.Verify(s => s.SetLocale(It.IsAny<string>()), Times.Never);
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
}
