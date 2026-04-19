// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.App.Tests;

public class FormsInputTypeTests : ComponentTestBase
{
    private static InstanceDto MakeInstance() =>
        new(Id: "1", Name: "Test", ModeKey: "MYTHIC", Expansion: "TWW");

    [Fact]
    public void CreateRunPage_StartTime_Is_DatetimeLocal_Input()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var instances = new Mock<IInstancesClient>();
        instances.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeInstance() });
        Services.AddSingleton(instances.Object);
        Services.AddSingleton(new Mock<IRunsClient>().Object);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
        {
            var input = cut.Find("input#starttime-input");
            Assert.Equal("datetime-local", input.GetAttribute("type"));
        });
    }

    [Fact]
    public void CreateRunPage_SignupClose_Is_DatetimeLocal_Input()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var instances = new Mock<IInstancesClient>();
        instances.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeInstance() });
        Services.AddSingleton(instances.Object);
        Services.AddSingleton(new Mock<IRunsClient>().Object);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
        {
            var input = cut.Find("input#signupclose-input");
            Assert.Equal("datetime-local", input.GetAttribute("type"));
        });
    }

    [Fact]
    public void CreateRunPage_ModeKey_Has_Autocapitalize_Characters()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var instances = new Mock<IInstancesClient>();
        instances.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeInstance() });
        Services.AddSingleton(instances.Object);
        Services.AddSingleton(new Mock<IRunsClient>().Object);

        var cut = Render<CreateRunPage>();

        // FluentTextField renders as a <fluent-text-field> web component in bUnit.
        // Unknown HTML attributes (autocapitalize) flow through to the element and
        // are verifiable via DOM. This confirms the ModeKeyAttrs splat is wired.
        cut.WaitForAssertion(() =>
        {
            var modeKey = cut.Find("#modekey-input");
            Assert.Equal("characters", modeKey.GetAttribute("autocapitalize"));
        });
    }
}
