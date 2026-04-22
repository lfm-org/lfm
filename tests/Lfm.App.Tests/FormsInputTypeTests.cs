// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Expansions;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.App.Tests;

public class FormsInputTypeTests : ComponentTestBase
{
    private static InstanceDto MakeInstance() =>
        new(Id: "1:MYTHIC_KEYSTONE:5",
            InstanceNumericId: 1,
            Name: "Test Dungeon",
            ModeKey: "MYTHIC_KEYSTONE:5",
            Expansion: "The War Within",
            Category: "DUNGEON",
            ExpansionId: 505,
            Difficulty: "MYTHIC_KEYSTONE",
            Size: 5);

    private void WireCreateRunServices()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var instances = new Mock<IInstancesClient>();
        instances.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeInstance() });
        Services.AddSingleton(instances.Object);

        var expansions = new Mock<IExpansionsClient>();
        expansions.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ExpansionDto(505, "The War Within") });
        Services.AddSingleton(expansions.Object);

        var guild = new Mock<IGuildClient>();
        guild.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);
        Services.AddSingleton(guild.Object);

        Services.AddSingleton(new Mock<IRunsClient>().Object);
    }

    [Fact]
    public void CreateRunPage_StartTime_Is_DatetimeLocal_Input()
    {
        WireCreateRunServices();

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
        {
            var input = cut.Find("input#starttime-input");
            Assert.Equal("datetime-local", input.GetAttribute("type"));
        });
    }

    [Fact]
    public void CreateRunPage_SignupClose_Collapsible_Reveals_DatetimeLocal_Input_After_Click()
    {
        // Signup-close is collapsed behind an "+ Add signup deadline" button on
        // the reshaped form — rarely-used affordance tucked away. Clicking it
        // must render the datetime-local input the test originally pinned.
        WireCreateRunServices();

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
        {
            // The input is NOT in the initial markup.
            Assert.Empty(cut.FindAll("input#signupclose-input"));
        });

        // Click the reveal button (matched by its localized label).
        var revealLabel = Loc("createRun.addSignupDeadline");
        var revealBtn = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(revealLabel));
        revealBtn.Click();

        cut.WaitForAssertion(() =>
        {
            var input = cut.Find("input#signupclose-input");
            Assert.Equal("datetime-local", input.GetAttribute("type"));
        });
    }
}
