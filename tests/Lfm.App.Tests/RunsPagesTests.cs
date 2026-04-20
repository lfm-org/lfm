// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
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

    // Anchored to UtcNow so these fixtures never become time bombs against a
    // future-dated assertion. See issue #49.
    private static readonly string FutureStartTime =
        DateTimeOffset.UtcNow.AddDays(30).ToString("o");
    private static readonly string FutureSignupCloseTime =
        DateTimeOffset.UtcNow.AddDays(30).AddHours(-2).ToString("o");
    private static readonly string PastCreatedAt =
        DateTimeOffset.UtcNow.AddDays(-14).ToString("o");

    private static RunSummaryDto MakeSummary(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: PastCreatedAt,
            Ttl: 604800,
            RunCharacters: []);

    private static RunDetailDto MakeDetail(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "heroic",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            CreatorGuildId: 42,
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            CreatorBattleNetId: "player#1234",
            CreatedAt: PastCreatedAt,
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

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
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
            Assert.Contains("Liberation of Undermine", cut.Markup));
    }

    [Fact]
    public void RunsPage_RunListItem_Has_Accessible_Name_Combining_Instance_And_Date()
    {
        // Screen-reader users navigating the run list hear a concise aria-label
        // ("<Instance> on <Date>") instead of the implicit multi-line span
        // concatenation. Pin the contract so a future refactor of the run-list
        // template doesn't silently regress it.
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            var runButton = cut.Find("button.run-list-item");
            var ariaLabel = runButton.GetAttribute("aria-label") ?? string.Empty;
            Assert.Contains("Liberation of Undermine", ariaLabel);
            Assert.Contains(" on ", ariaLabel);
        });
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
            Assert.Contains(Loc("runs.empty"), cut.Markup));
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
            Assert.Contains("Network error", cut.Markup));
    }

    private static RunCharacterDto MakeCharacter(
        string id,
        string name,
        int classId = 8,
        string className = "Mage",
        string? role = "DPS",
        string attendance = "IN",
        string? spec = "Arcane") =>
        new(
            Id: id,
            CharacterId: $"char-{id}",
            CharacterName: name,
            CharacterRealm: "Test Realm",
            CharacterLevel: 80,
            CharacterClassId: classId,
            CharacterClassName: className,
            CharacterRaceId: 1,
            CharacterRaceName: "Human",
            DesiredAttendance: attendance,
            ReviewedAttendance: attendance,
            SpecId: 62,
            SpecName: spec,
            Role: role,
            IsCurrentUser: false);

    private static RunDetailDto MakeDetailWithRoster(IReadOnlyList<RunCharacterDto> characters) =>
        MakeDetail() with { RunCharacters = characters };

    [Fact]
    public void RunsPage_RoleColumns_SplitAttendingCharactersByRole()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("c-tank", "Tankington", classId: 1, className: "Warrior", role: "TANK", spec: "Protection"),
                MakeCharacter("c-heal", "Healsworth", classId: 2, className: "Paladin", role: "HEALER", spec: "Holy"),
                MakeCharacter("c-dps", "Dpsalot", classId: 8, className: "Mage", role: "DPS", spec: "Frost"),
            }));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Equal(3, cut.FindAll(".character-row").Count));

        var attendingTitle = cut.Find("[data-testid='roster-attending-title']");
        Assert.Contains("(3)", attendingTitle.TextContent);

        var roleColumns = cut.FindAll(".roster-role-column");
        Assert.Equal(3, roleColumns.Count);

        var markup = cut.Markup;
        Assert.Contains("Tankington", markup);
        Assert.Contains("Healsworth", markup);
        Assert.Contains("Dpsalot", markup);
    }

    [Fact]
    public void RunsPage_NotAttendingSection_RendersOutAndAwayCharacters()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("c-in", "Present", attendance: "IN"),
                MakeCharacter("c-out", "Skipper", attendance: "OUT"),
                MakeCharacter("c-away", "Traveler", attendance: "AWAY"),
            }));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("[data-testid='roster-not-attending-title']")));

        var notAttendingTitle = cut.Find("[data-testid='roster-not-attending-title']");
        Assert.Contains("(2)", notAttendingTitle.TextContent);

        Assert.Contains("Skipper", cut.Markup);
        Assert.Contains("Traveler", cut.Markup);
        Assert.Contains("attendance-pill--out", cut.Markup);
        Assert.Contains("attendance-pill--away", cut.Markup);
    }

    [Fact]
    public void RunsPage_CharacterRow_UsesClassColorBorder()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("c-mage", "Frostmage", classId: 8, className: "Mage"),
            }));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find(".character-row")));

        var row = cut.Find(".character-row");
        var style = row.GetAttribute("style") ?? "";
        // Mage class color is #3FC7EB (see Lfm.Contracts/WoW/WowClasses.cs)
        Assert.Contains("#3FC7EB", style);
    }

    [Fact]
    public void RunsPage_RunListItem_RendersDifficultyPillAndAttendingFooter()
    {
        var client = new Mock<IRunsClient>();
        var summary = MakeSummary() with { ModeKey = "MYTHIC:25" };
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto>
            {
                summary with
                {
                    RunCharacters = new List<RunCharacterDto>
                    {
                        MakeCharacter("c1", "A", attendance: "IN"),
                        MakeCharacter("c2", "B", attendance: "IN"),
                        MakeCharacter("c3", "C", attendance: "OUT"),
                    },
                },
            });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find(".difficulty-pill--mythic")));

        Assert.Contains("Mythic", cut.Find(".difficulty-pill--mythic").TextContent);
        // Footer format: "{attending} attending · {total} signed up" — seeded IN count is 2, total 3.
        Assert.Contains("2 attending", cut.Markup);
        Assert.Contains("3 signed up", cut.Markup);
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

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
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
            Assert.Contains(Loc("createRun.title"), cut.Markup));
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
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
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

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
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
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
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
            Assert.Contains(Loc("editRun.error.notFound"), cut.Markup));
    }

    // Regression: save without editing datetime fields must preserve the
    // original UTC ISO string byte-for-byte. A prior migration to native
    // <input type="datetime-local"> stripped the timezone on round-trip,
    // causing the API's locked-field check to reject description-only edits
    // on runs with signups (400 "Cannot change start time after signups").
    [Fact]
    public void EditRunPage_Save_Without_Edit_Preserves_Utc_Iso_Round_Trip()
    {
        const string OriginalStart = "2026-05-20T15:30:45.1234567Z";
        const string OriginalSignup = "2026-05-20T15:00:45.1234567Z";

        var instancesClient = new Mock<IInstancesClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InstanceDto> { new("1", "Liberation of Undermine", "raid", "tww") });

        UpdateRunRequest? captured = null;
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail() with { StartTime = OriginalStart, SignupCloseTime = OriginalSignup });
        runsClient.Setup(c => c.UpdateAsync("run-1", It.IsAny<UpdateRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, UpdateRunRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync((RunDetailDto?)null);

        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));

        var saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.saveChanges")));
        saveButton.Click();

        cut.WaitForAssertion(() => Assert.NotNull(captured));
        Assert.Equal(OriginalStart, captured!.StartTime);
        Assert.Equal(OriginalSignup, captured.SignupCloseTime);
    }

    // RD-DIALOG-1 (#26): the delete confirmation renders as a native <dialog>
    // so focus trap, Esc dismissal, and focus restoration come from the browser
    // for free. Clicking "Delete run" must open it via the dialog.js interop
    // module rather than flipping a render flag.
    [Fact]
    public void EditRunPage_Delete_Button_Opens_Native_Dialog_Via_Interop()
    {
        var instancesClient = new Mock<IInstancesClient>();
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<InstanceDto>());
        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(runsClient.Object);

        var dialogModule = JSInterop.SetupModule("./js/dialog.js");

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.deleteRun"), cut.Markup));

        var dialog = cut.Find("dialog");
        Assert.Equal("delete-dialog-title", dialog.GetAttribute("aria-labelledby"));
        Assert.Equal("delete-dialog-body", dialog.GetAttribute("aria-describedby"));

        var deleteButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.deleteRun")));
        deleteButton.Click();

        cut.WaitForAssertion(() => dialogModule.VerifyInvoke("showModal"));
    }
}
