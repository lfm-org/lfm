// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Expansions;
using Lfm.Contracts.Guild;
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

    private static RunSummaryDto MakeSummary(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "HEROIC:25",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            RunCharacters: [],
            Difficulty: "HEROIC",
            Size: 25);

    private static RunDetailDto MakeDetail(string id = "run-1") =>
        new(
            Id: id,
            StartTime: FutureStartTime,
            SignupCloseTime: FutureSignupCloseTime,
            Description: "Test run",
            ModeKey: "HEROIC:25",
            Visibility: "PUBLIC",
            CreatorGuild: "Stormchasers",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            RunCharacters: [],
            Difficulty: "HEROIC",
            Size: 25);

    private static InstanceDto MakeInstanceFixture() =>
        new("1:HEROIC:25", 1, "Liberation of Undermine", "HEROIC:25",
            "The War Within", "RAID", 505, "HEROIC", 25);

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
        // driven by the localized `runs.listItemAriaLabel` template instead of
        // the implicit multi-line span concatenation. Pin the exact localized
        // output so a template refactor or locale drift can't silently regress
        // the name without the test noticing.
        var summary = MakeSummary();
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { summary });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            var runButton = cut.Find("button.run-list-item");
            var ariaLabel = runButton.GetAttribute("aria-label") ?? string.Empty;
            // FormatDate (private in RunsPage) parses the ISO string and
            // formats as "yyyy-MM-dd HH:mm" with InvariantCulture; mirror that
            // here so the test pins the exact localized output screen readers
            // get, without reaching into the page object's private helpers.
            var formattedDate = DateTimeOffset.Parse(summary.StartTime, System.Globalization.CultureInfo.InvariantCulture)
                .ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            var expected = Loc("runs.listItemAriaLabel", summary.InstanceName ?? "", formattedDate);
            Assert.Equal(expected, ariaLabel);
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
        string name,
        int classId = 8,
        string className = "Mage",
        string? role = "DPS",
        string attendance = "IN",
        string? spec = "Arcane") =>
        new(
            CharacterName: name,
            CharacterRealm: "Test Realm",
            CharacterClassId: classId,
            CharacterClassName: className,
            DesiredAttendance: attendance,
            ReviewedAttendance: attendance,
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
                MakeCharacter("Tankington", classId: 1, className: "Warrior", role: "TANK", spec: "Protection"),
                MakeCharacter("Healsworth", classId: 2, className: "Paladin", role: "HEALER", spec: "Holy"),
                MakeCharacter("Dpsalot", classId: 8, className: "Mage", role: "DPS", spec: "Frost"),
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
                MakeCharacter("Present", attendance: "IN"),
                MakeCharacter("Skipper", attendance: "OUT"),
                MakeCharacter("Traveler", attendance: "AWAY"),
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
                MakeCharacter("Frostmage", classId: 8, className: "Mage"),
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
    public void RunsPage_RunListItem_RendersDifficultyPillAndCompositionSummary()
    {
        var client = new Mock<IRunsClient>();
        // Seed 2 DPS attending (IN) and 1 DPS OUT in a MYTHIC 25-man raid.
        // Standard composition target for 25-man is 2T / 5H / 18D, so the
        // rendered composition summary is "T 0/2 · H 0/5 · D 2/18" with the
        // tank + healer slots carrying the shortage modifier class.
        var summary = MakeSummary() with { Difficulty = "MYTHIC", Size = 25, ModeKey = "MYTHIC:25" };
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto>
            {
                summary with
                {
                    RunCharacters = new List<RunCharacterDto>
                    {
                        MakeCharacter("A", attendance: "IN"),
                        MakeCharacter("B", attendance: "IN"),
                        MakeCharacter("C", attendance: "OUT"),
                    },
                },
            });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find(".difficulty-pill--mythic")));

        Assert.Contains("Mythic", cut.Find(".difficulty-pill--mythic").TextContent);

        var composition = cut.Find(".run-list-item__composition");
        Assert.Contains("0/2", composition.TextContent);
        Assert.Contains("0/5", composition.TextContent);
        Assert.Contains("2/18", composition.TextContent);

        // All three role slots are under-target for this partially-filled
        // 25-man so each carries the shortage modifier.
        var slots = cut.FindAll(".run-list-item__roleslot");
        Assert.Equal(3, slots.Count);
        Assert.All(slots, s => Assert.Contains("run-list-item__roleslot--short", s.ClassName ?? ""));

        // Difficulty + kind drive data-attributes on the item so CSS can
        // stripe the left edge from `data-kind` without inline style
        // overrides leaking into tests. `data-difficulty` is retained for
        // E2E locators and future theming even though only `data-kind`
        // currently feeds a CSS selector.
        var item = cut.Find("button.run-list-item");
        Assert.Equal("mythic", item.GetAttribute("data-difficulty"));
        Assert.Equal("raid", item.GetAttribute("data-kind"));
        Assert.False(item.HasAttribute("style"));
    }

    // ── CreateRunPage ────────────────────────────────────────────────────────

    private void WireCreateRunServices(
        IReadOnlyList<InstanceDto>? instances = null,
        IReadOnlyList<ExpansionDto>? expansions = null,
        GuildDto? guild = null,
        TaskCompletionSource<IReadOnlyList<InstanceDto>>? instancesPending = null)
    {
        var instancesClient = new Mock<IInstancesClient>();
        if (instancesPending is not null)
            instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
                .Returns(instancesPending.Task);
        else
            instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(instances ?? []);
        Services.AddSingleton(instancesClient.Object);

        var expansionsClient = new Mock<IExpansionsClient>();
        expansionsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expansions ?? [new ExpansionDto(505, "The War Within")]);
        Services.AddSingleton(expansionsClient.Object);

        var guildClient = new Mock<IGuildClient>();
        guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);
        Services.AddSingleton(guildClient.Object);

        Services.AddSingleton(new Mock<IRunsClient>().Object);
    }

    [Fact]
    public void CreateRunPage_Renders_Loading_Ring_On_Mount()
    {
        WireCreateRunServices(instancesPending: new TaskCompletionSource<IReadOnlyList<InstanceDto>>());

        var cut = Render<CreateRunPage>();

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void CreateRunPage_Renders_Form_After_Load()
    {
        WireCreateRunServices(instances: new List<InstanceDto>
        {
            new("1:MYTHIC_KEYSTONE:5", 1, "Ara-Kara", "MYTHIC_KEYSTONE:5",
                "The War Within", "DUNGEON", 505, "MYTHIC_KEYSTONE", 5),
        });

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.title"), cut.Markup));
    }

    [Fact]
    public void CreateRunPage_Renders_Create_Button()
    {
        WireCreateRunServices();

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
    }

    // ── EditRunPage ──────────────────────────────────────────────────────────

    private Mock<IRunsClient> WireEditRunServices(
        Mock<IRunsClient>? runsClient = null,
        IReadOnlyList<InstanceDto>? instances = null,
        IReadOnlyList<ExpansionDto>? expansions = null,
        GuildDto? guild = null)
    {
        runsClient ??= new Mock<IRunsClient>();
        var instancesClient = new Mock<IInstancesClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances ?? [MakeInstanceFixture()]);
        var expansionsClient = new Mock<IExpansionsClient>();
        expansionsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expansions ?? [new ExpansionDto(505, "The War Within")]);
        var guildClient = new Mock<IGuildClient>();
        guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(guild);

        Services.AddSingleton(instancesClient.Object);
        Services.AddSingleton(expansionsClient.Object);
        Services.AddSingleton(guildClient.Object);
        Services.AddSingleton(runsClient.Object);
        return runsClient;
    }

    [Fact]
    public void EditRunPage_Renders_Loading_Ring_On_Mount()
    {
        var runsClient = new Mock<IRunsClient>();
        var tcs = new TaskCompletionSource<RunDetailDto?>();
        runsClient.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(tcs.Task);
        WireEditRunServices(runsClient);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void EditRunPage_Renders_Form_After_Run_Loads()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        WireEditRunServices(runsClient);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
    }

    [Fact]
    public void EditRunPage_Shows_Error_When_Run_Not_Found()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDetailDto?)null);
        WireEditRunServices(runsClient, instances: []);

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

        UpdateRunRequest? captured = null;
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail() with { StartTime = OriginalStart, SignupCloseTime = OriginalSignup });
        runsClient.Setup(c => c.UpdateAsync("run-1", It.IsAny<UpdateRunRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, UpdateRunRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync((RunDetailDto?)null);
        WireEditRunServices(runsClient);

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
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        WireEditRunServices(runsClient, instances: []);

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
