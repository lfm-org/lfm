// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Tests;

public class RunsPageKeyboardTests : ComponentTestBase
{
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");

    private static RunSummaryDto MakeRun(string id, string name) =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "",
            Visibility: "GUILD",
            CreatorGuild: "Test",
            InstanceId: 1,
            InstanceName: name,
            RunCharacters: Array.Empty<RunCharacterDto>(),
            Difficulty: "MYTHIC",
            Size: 20);

    [Fact]
    public void RunsPage_Run_List_Item_Is_A_Button()
    {
        this.AddAuthorization().SetAuthorized("player#1234");
        var runs = new Mock<IRunsClient>();
        runs.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { MakeRun("r1", "Mythic Test") });
        Services.AddSingleton(runs.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            // The list row MUST be a <button>, not a <div>.
            var buttons = cut.FindAll("button.run-list-item");
            Assert.NotEmpty(buttons);

            var divs = cut.FindAll("div.run-list-item");
            Assert.Empty(divs);
        });
    }
}
