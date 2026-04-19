// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Instances;
using Lfm.Contracts.Me;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Tests;

public class HeadingStructureTests : ComponentTestBase
{
    private void WireAuthStubs()
    {
        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<CharacterDto>?)null);
        battleNet.Setup(c => c.GetPortraitsAsync(It.IsAny<IEnumerable<CharacterPortraitRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDictionary<string, string>?)null);
        var me = new Mock<IMeClient>();
        me.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MeResponse?)null);
        var guild = new Mock<IGuildClient>();
        guild.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);
        var runs = new Mock<IRunsClient>();
        runs.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RunSummaryDto>());
        var instances = new Mock<IInstancesClient>();
        instances.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InstanceDto>());

        Services.AddSingleton(battleNet.Object);
        Services.AddSingleton(me.Object);
        Services.AddSingleton(guild.Object);
        Services.AddSingleton(runs.Object);
        Services.AddSingleton(instances.Object);
    }

    [Theory]
    [InlineData(typeof(LandingPage))]
    [InlineData(typeof(LoginPage))]
    [InlineData(typeof(LoginFailedPage))]
    [InlineData(typeof(GoodbyePage))]
    [InlineData(typeof(PrivacyPolicyPage))]
    [InlineData(typeof(NotFound))]
    public void UnauthenticatedPage_Renders_Exactly_One_H1(Type pageType)
    {
        this.AddAuthorization();
        WireAuthStubs();

        var cut = this.GetType().GetMethod(nameof(RenderByType), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(pageType)
            .Invoke(this, null)!;
        var markup = (string)cut.GetType().GetProperty("Markup")!.GetValue(cut)!;

        var h1Count = System.Text.RegularExpressions.Regex.Matches(markup, "<h1(\\s|>)").Count;
        Assert.Equal(1, h1Count);
    }

    private IRenderedComponent<T> RenderByType<T>() where T : Microsoft.AspNetCore.Components.IComponent
        => Render<T>();

    [Theory]
    [InlineData(typeof(RunsPage))]
    [InlineData(typeof(CharactersPage))]
    [InlineData(typeof(GuildPage))]
    [InlineData(typeof(GuildAdminPage))]
    [InlineData(typeof(CreateRunPage))]
    [InlineData(typeof(EditRunPage))]
    [InlineData(typeof(InstancesPage))]
    public void AuthenticatedPage_Renders_Exactly_One_H1(Type pageType)
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        WireAuthStubs();

        var cut = this.GetType().GetMethod(nameof(RenderByType), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(pageType)
            .Invoke(this, null)!;
        var markup = (string)cut.GetType().GetProperty("Markup")!.GetValue(cut)!;

        var h1Count = System.Text.RegularExpressions.Regex.Matches(markup, "<h1(\\s|>)").Count;
        Assert.Equal(1, h1Count);
    }
}
