using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Instances;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Tests;

public class RunsPagesTests : ComponentTestBase
{
    // ── Shared helpers ───────────────────────────────────────────────────────

    private static RunSummaryDto MakeSummary(string id = "run-1") =>
        new(
            Id: id,
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 604800,
            RunCharacters: []);

    private static RunDetailDto MakeDetail(string id = "run-1") =>
        new(
            Id: id,
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "2026-05-01T18:00:00Z",
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: "2026-04-01T10:00:00Z",
            Ttl: 604800,
            RunCharacters: []);

    // ── RunsPage ─────────────────────────────────────────────────────────────

    [Fact]
    public void RunsPage_Renders_Loading_Ring_On_Mount()
    {
        var client = new Mock<IRunsClient>();
        var tcs = new TaskCompletionSource<IReadOnlyList<RunSummaryDto>>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.FindAll("fluent-progress-ring").Should().NotBeEmpty();
    }

    [Fact]
    public void RunsPage_Renders_Run_List_After_Load()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Liberation of Undermine"));
    }

    [Fact]
    public void RunsPage_Renders_Empty_State_When_No_Runs()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto>());
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("runs.empty"));
    }

    [Fact]
    public void RunsPage_Renders_Error_Message_On_Failure()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Network error"));
    }

    // ── CreateRunPage ────────────────────────────────────────────────────────

    [Fact]
    public void CreateRunPage_Renders_Loading_Ring_On_Mount()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        var tcs = new TaskCompletionSource<IReadOnlyList<InstanceDto>>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<CreateRunPage>();

        cut.FindAll("fluent-progress-ring").Should().NotBeEmpty();
    }

    [Fact]
    public void CreateRunPage_Renders_Form_After_Instances_Load()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto> { new("1", "Liberation of Undermine", "raid", "tww") });
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("createRun.title"));
    }

    [Fact]
    public void CreateRunPage_Renders_Create_Button()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>());
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("createRun.submit"));
    }

    // ── EditRunPage ──────────────────────────────────────────────────────────

    [Fact]
    public void EditRunPage_Renders_Loading_Ring_On_Mount()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        var tcs = new TaskCompletionSource<RunDetailDto?>();
        runsClient.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(tcs.Task);
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>());
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.FindAll("fluent-progress-ring").Should().NotBeEmpty();
    }

    [Fact]
    public void EditRunPage_Renders_Form_After_Run_Loads()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto> { new("1", "Liberation of Undermine", "raid", "tww") });
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("editRun.saveChanges"));
    }

    [Fact]
    public void EditRunPage_Shows_Error_When_Run_Not_Found()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDetailDto?)null);
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto>());
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "missing-id"));

        cut.WaitForAssertion(() =>
            cut.Markup.Should().Contain("Run not found."));
    }
}
