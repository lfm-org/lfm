// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Bunit.TestDoubles;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Lfm.App;
using Lfm.App.Components;
using Moq;
using Lfm.App.Components.Runs;
using Lfm.App.Pages;
using Lfm.App.Services;
using Lfm.Contracts.Characters;
using Lfm.Contracts.Expansions;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Instances;
using Lfm.Contracts.Me;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Tests;

public class RunsPagesTests : ComponentTestBase
{
    // ── Shared helpers ───────────────────────────────────────────────────────

    // Mirrors the same constant in CreateRunPage/EditRunPage — see those
    // pages' `ResolveCurrentSeasonId` helper. Kept local so a rename in the
    // page breaks this test before the form silently reverts behavior.
    private const string CurrentSeasonExpansion = "Current Season";

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
            Visibility: "GUILD",
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
            Visibility: "GUILD",
            CreatorGuild: "Stormchasers",
            InstanceId: 1,
            InstanceName: "Liberation of Undermine",
            RunCharacters: [],
            Difficulty: "HEROIC",
            Size: 25);

    private static InstanceDto MakeInstanceFixture() =>
        new("1:HEROIC:25", 1, "Liberation of Undermine", "HEROIC:25",
            "The War Within", "RAID", 505, "HEROIC", 25);

    private static GuildDto MakeGuildDto(
        bool canCreateGuildRuns = true,
        bool canEdit = false,
        bool requiresSetup = false,
        bool isInitialized = true,
        bool rankDataFresh = true) =>
        new(
            Guild: new GuildInfoDto(
                Id: 12345,
                Name: "Stormchasers",
                Slogan: null,
                RealmName: "Silvermoon",
                FactionName: "Alliance",
                MemberCount: 12,
                RankCount: 8,
                CrestEmblemUrl: null,
                CrestBorderUrl: null),
            Setup: new GuildSetupDto(
                IsInitialized: isInitialized,
                RequiresSetup: requiresSetup,
                RankDataFresh: rankDataFresh,
                Timezone: "Europe/Helsinki",
                Locale: "en-gb"),
            Settings: null,
            Editor: new GuildEditorDto(CanEdit: canEdit),
            MemberPermissions: new GuildMemberPermissionsDto(
                CanCreateGuildRuns: canCreateGuildRuns,
                CanSignupGuildRuns: true,
                CanDeleteGuildRuns: false));

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
    public void RunsPage_LoadMore_Appends_Runs()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListPageAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsListResponse([MakeSummary("run-1")], "next-token"));
        client.Setup(c => c.ListPageAsync("next-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsListResponse([MakeSummary("run-2") with { InstanceName = "Nerub-ar Palace" }], null));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Liberation of Undermine", cut.Markup);
            Assert.Contains(Loc("runs.loadMore"), cut.Markup);
        });

        var loadMore = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("runs.loadMore"), StringComparison.Ordinal));
        loadMore.Click();

        cut.WaitForAssertion(() =>
        {
            client.Verify(c => c.ListPageAsync("next-token", It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Liberation of Undermine", cut.Markup);
            Assert.Contains("Nerub-ar Palace", cut.Markup);
            Assert.DoesNotContain(Loc("runs.loadMore"), cut.Markup);
        });
    }

    [Fact]
    public void RunsPage_Empty_Page_With_Continuation_Can_Load_More()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListPageAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsListResponse([], "next-token"));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("runs.empty"), cut.Markup);
            Assert.Contains(Loc("runs.loadMore"), cut.Markup);
        });
    }

    [Fact]
    public void RunsPage_LoadMore_Failure_Preserves_Current_List_And_Shows_Error()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListPageAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunsListResponse([MakeSummary("run-1")], "next-token"));
        client.Setup(c => c.ListPageAsync("next-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("More error"));
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Liberation of Undermine", cut.Markup);
            Assert.Contains(Loc("runs.loadMore"), cut.Markup);
        });

        var loadMore = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("runs.loadMore"), StringComparison.Ordinal));
        loadMore.Click();

        cut.WaitForAssertion(() =>
        {
            client.Verify(c => c.ListPageAsync("next-token", It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains("Liberation of Undermine", cut.Markup);
            Assert.Contains("More error", cut.Markup);
            Assert.Contains(Loc("runs.loadMore"), cut.Markup);
        });
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
        string? spec = "Arcane",
        bool isCurrentUser = false,
        string? characterId = null,
        int? specId = null) =>
        new(
            CharacterId: characterId ?? $"eu-test-realm-{name.ToLowerInvariant()}",
            CharacterName: name,
            CharacterRealm: "Test Realm",
            CharacterClassId: classId,
            CharacterClassName: className,
            DesiredAttendance: attendance,
            ReviewedAttendance: attendance,
            SpecId: specId,
            SpecName: spec,
            Role: role,
            IsCurrentUser: isCurrentUser);

    private static RunDetailDto MakeDetailWithRoster(IReadOnlyList<RunCharacterDto> characters) =>
        MakeDetail() with { RunCharacters = characters };

    private static CharacterDto MakeAppCharacter(
        string name = "Aelrin",
        string realm = "silvermoon",
        int? activeSpecId = 257,
        IReadOnlyList<CharacterSpecializationDto>? specializations = null) =>
        new(
            Name: name,
            Realm: realm,
            RealmName: "Silvermoon",
            Level: 80,
            Region: "eu",
            ClassId: 5,
            ClassName: "Priest",
            PortraitUrl: null,
            ActiveSpecId: activeSpecId,
            SpecName: "Holy",
            Specializations: specializations);

    private void WireSignupSupport(
        Mock<IRunsClient>? runsClient = null,
        IReadOnlyList<CharacterDto>? characters = null,
        string? selectedCharacterId = "eu-silvermoon-aelrin")
    {
        var signupCharacters = characters ?? new List<CharacterDto> { MakeAppCharacter() };
        runsClient?.Setup(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(signupCharacters));

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(signupCharacters);
        Services.AddSingleton(battleNet.Object);

        var me = new Mock<IMeClient>();
        me.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeResponse("bnet-1", null, selectedCharacterId, null, false, "en"));
        Services.AddSingleton(me.Object);
    }

    private void WireAppRouteServices(
        GuildDto? guild,
        TaskCompletionSource<GuildDto?>? guildPending = null)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");

        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        Services.AddSingleton(runsClient.Object);

        var instancesClient = new Mock<IInstancesClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([MakeInstanceFixture()]);
        Services.AddSingleton(instancesClient.Object);

        var expansionsClient = new Mock<IExpansionsClient>();
        expansionsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpansionDto(505, "The War Within")]);
        Services.AddSingleton(expansionsClient.Object);

        var guildClient = new Mock<IGuildClient>();
        if (guildPending is not null)
        {
            guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                .Returns(guildPending.Task);
        }
        else
        {
            guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(guild);
        }

        Services.AddSingleton(guildClient.Object);
    }

    [Theory]
    [InlineData("/runs")]
    [InlineData("/runs/new")]
    [InlineData("/runs/run-1/edit")]
    public void GuildSetupGate_Redirects_Run_Routes_For_Editor_SetupRequired_Guild(string route)
    {
        WireAppRouteServices(MakeGuildDto(canEdit: true, requiresSetup: true, isInitialized: false));
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo(route);

        var cut = Render<App>();

        cut.WaitForAssertion(() =>
            Assert.Equal("/guild?setup=required", new Uri(nav.Uri).PathAndQuery));
    }

    [Fact]
    public void GuildSetupGate_Does_Not_Fetch_Guild_Before_Unauthorized_Runs_Redirect()
    {
        this.AddAuthorization();
        var guildClient = new Mock<IGuildClient>();
        guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(canEdit: true, requiresSetup: true, isInitialized: false));
        Services.AddSingleton(guildClient.Object);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/runs");

        var cut = Render<App>();

        cut.WaitForAssertion(() =>
            Assert.Equal("/login?redirect=%2Fruns", new Uri(nav.Uri).PathAndQuery));
        guildClient.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GuildSetupGate_Does_Not_Redirect_NonEditors()
    {
        WireAppRouteServices(MakeGuildDto(canEdit: false, requiresSetup: true, isInitialized: false));
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/runs");

        var cut = Render<App>();

        cut.WaitForAssertion(() =>
            Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath));
        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.empty"), cut.Markup));
    }

    [Fact]
    public void GuildSetupGate_Shows_Loading_State_While_Guild_Fetch_Is_Pending()
    {
        WireAppRouteServices(guild: null, guildPending: new TaskCompletionSource<GuildDto?>());
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/runs");

        var cut = Render<App>();

        Assert.Contains(Loc("guild.checkingSetup"), cut.Markup);
        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
        Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath);
    }

    [Fact]
    public void GuildSetupGate_Shows_Loading_And_Does_Not_Render_Runs_While_Auth_Is_Pending()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(new PendingAuthenticationStateProvider());
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Services.AddSingleton(runsClient.Object);
        var guildClient = new Mock<IGuildClient>();
        guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto(canEdit: true, requiresSetup: true, isInitialized: false));
        Services.AddSingleton(guildClient.Object);
        var nav = Services.GetRequiredService<BunitNavigationManager>();
        nav.NavigateTo("/runs");

        var cut = Render<App>();

        Assert.Contains(Loc("guild.checkingSetup"), cut.Markup);
        runsClient.Verify(c => c.ListAsync(It.IsAny<CancellationToken>()), Times.Never);
        guildClient.Verify(c => c.GetAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private IElement FindSignupSelect(IRenderedComponent<RunsPage> cut, string label)
    {
        var selects = cut.FindAll("fluent-select");
        var labelled = selects
            .Where(select =>
                select.GetAttribute("label") == label ||
                select.TextContent.Contains(label, StringComparison.Ordinal))
            .ToList();
        if (labelled.Count == 1)
            return labelled[0];

        var index = label == Loc("runs.signup.character") ? 0 : 1;
        Assert.True(selects.Count > index);
        return selects[index];
    }

    private IElement FindSignupAttendanceGroup(IRenderedComponent<RunsPage> cut)
    {
        var group = cut.FindAll("[role='radiogroup']").Single(group =>
        {
            var labelId = group.GetAttribute("aria-labelledby");
            if (string.IsNullOrEmpty(labelId))
                return false;

            var label = cut.Find($"#{labelId}");
            return label.TextContent.Trim() == Loc("runs.signup.attendance");
        });

        Assert.Contains("run-signup-attendance-toggle", group.ClassName ?? string.Empty);
        return group;
    }

    private sealed class PendingAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly TaskCompletionSource<AuthenticationState> _pending = new();

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            _pending.Task;
    }

    [Fact]
    public void RunsPage_Signup_Submits_Selected_Character_And_Updates_Roster()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("Aelrin", classId: 5, className: "Priest", role: "HEALER", spec: "Holy", isCurrentUser: true),
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.action"), cut.Markup));

        var signupButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.action"), StringComparison.Ordinal));
        signupButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "IN" &&
                    r.SpecId == 257),
                It.IsAny<CancellationToken>()),
                Times.Once));
        cut.WaitForAssertion(() => Assert.Contains("Aelrin", cut.Markup));
    }

    [Fact]
    public void RunsPage_Signup_Renders_Attendance_As_Radiogroup()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.action"), cut.Markup));

        var group = FindSignupAttendanceGroup(cut);
        var labelId = group.GetAttribute("aria-labelledby");
        Assert.False(string.IsNullOrEmpty(labelId));
        Assert.Equal(Loc("runs.signup.attendance"), cut.Find($"#{labelId}").TextContent.Trim());

        var radios = group.QuerySelectorAll("[role='radio']");
        Assert.Equal(5, radios.Count);
        Assert.Equal(Loc("runs.attendance.in"), radios[0].TextContent.Trim());
        Assert.Equal(Loc("runs.attendance.late"), radios[1].TextContent.Trim());
        Assert.Equal(Loc("runs.attendance.bench"), radios[2].TextContent.Trim());
        Assert.Equal(Loc("runs.attendance.away"), radios[3].TextContent.Trim());
        Assert.Equal(Loc("runs.attendance.out"), radios[4].TextContent.Trim());
        Assert.Equal("true", radios[0].GetAttribute("aria-checked"));
        Assert.Empty(cut.FindAll("#signup-attendance-select"));
    }

    [Fact]
    public void RunsPage_Signup_Submits_Attendance_Selected_From_Radiogroup()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("Aelrin", classId: 5, className: "Priest", role: "HEALER", spec: "Holy", isCurrentUser: true),
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.action"), cut.Markup));

        var bench = cut.FindAll("[role='radio']")
            .Single(r => r.TextContent.Trim() == Loc("runs.attendance.bench"));
        bench.Click();

        var signupButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.action"), StringComparison.Ordinal));
        signupButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "BENCH" &&
                    r.SpecId == 257),
                It.IsAny<CancellationToken>()),
                Times.Once));
    }

    [Fact]
    public void RunsPage_CurrentSignup_Can_Change_Attendance_Without_Canceling()
    {
        var currentSignup = MakeCharacter(
            "Aelrin",
            classId: 5,
            className: "Priest",
            role: "HEALER",
            attendance: "IN",
            spec: "Holy",
            isCurrentUser: true,
            characterId: "eu-silvermoon-aelrin",
            specId: 257);
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                currentSignup with { DesiredAttendance = "BENCH", ReviewedAttendance = "BENCH" },
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.cancel"), cut.Markup));

        var selectedAttendance = cut.FindAll("[role='radio']")
            .Single(r => r.TextContent.Trim() == Loc("runs.attendance.in"));
        selectedAttendance.Click();
        client.Verify(c => c.SignupAsync(
            It.IsAny<string>(),
            It.IsAny<SignupRequest>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        var bench = cut.FindAll("[role='radio']")
            .Single(r => r.TextContent.Trim() == Loc("runs.attendance.bench"));
        bench.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "BENCH" &&
                    r.SpecId == 257),
                It.IsAny<CancellationToken>()),
                Times.Once));
        client.Verify(c => c.CancelSignupAsync("run-1", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RunsPage_CurrentSignup_Can_Open_ChangeCharacter_Panel()
    {
        var currentSignup = MakeCharacter(
            "Aelrin",
            classId: 5,
            className: "Priest",
            role: "HEALER",
            attendance: "IN",
            spec: "Holy",
            isCurrentUser: true,
            characterId: "eu-silvermoon-aelrin",
            specId: 257);
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                currentSignup with { SpecId = 258, SpecName = "Shadow" },
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(
            client,
            characters:
            [
                MakeAppCharacter(
                    activeSpecId: 257,
                    specializations:
                    [
                        new CharacterSpecializationDto(257, "Holy"),
                        new CharacterSpecializationDto(258, "Shadow"),
                    ]),
                MakeAppCharacter(
                    name: "Borin",
                    activeSpecId: 71,
                    specializations:
                    [
                        new CharacterSpecializationDto(71, "Arms"),
                        new CharacterSpecializationDto(72, "Fury"),
                    ]),
            ],
            selectedCharacterId: "eu-silvermoon-borin");

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.changeCharacter"), cut.Markup));

        var changeButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.changeCharacter"), StringComparison.Ordinal));
        changeButton.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("runs.signup.back"), cut.Markup);
            Assert.NotNull(FindSignupSelect(cut, Loc("runs.signup.character")));
            Assert.NotNull(FindSignupSelect(cut, Loc("runs.signup.spec")));
            Assert.Contains(Loc("runs.signup.spec"), cut.Markup);
            client.Verify(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        });

        Assert.Contains("Aelrin - Silvermoon", FindSignupSelect(cut, Loc("runs.signup.character")).TextContent);
        Assert.Contains("Holy", FindSignupSelect(cut, Loc("runs.signup.spec")).TextContent);
        Assert.Contains("Shadow", FindSignupSelect(cut, Loc("runs.signup.spec")).TextContent);

        FindSignupSelect(cut, Loc("runs.signup.spec")).Change("258");
        var submitButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.action"), StringComparison.Ordinal));
        submitButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "IN" &&
                    r.SpecId == 258),
                It.IsAny<CancellationToken>()),
                Times.Once));
    }

    [Fact]
    public void RunsPage_CurrentSignup_Editor_Submits_Current_Spec_When_ActiveSpecDiffers()
    {
        var currentSignup = MakeCharacter(
            "Aelrin",
            classId: 5,
            className: "Priest",
            role: "HEALER",
            attendance: "IN",
            spec: "Shadow",
            isCurrentUser: true,
            characterId: "eu-silvermoon-aelrin",
            specId: 258);
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(
            client,
            characters:
            [
                MakeAppCharacter(
                    activeSpecId: 257,
                    specializations:
                    [
                        new CharacterSpecializationDto(257, "Holy"),
                        new CharacterSpecializationDto(258, "Shadow"),
                    ]),
            ]);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.changeCharacter"), cut.Markup));

        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.changeCharacter"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Shadow", FindSignupSelect(cut, Loc("runs.signup.spec")).TextContent));

        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.action"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "IN" &&
                    r.SpecId == 258),
                It.IsAny<CancellationToken>()),
                Times.Once));
    }

    [Fact]
    public void RunsPage_CurrentSignup_Back_Discards_Unsaved_Character_And_Spec_Edits()
    {
        var currentSignup = MakeCharacter(
            "Aelrin",
            classId: 5,
            className: "Priest",
            role: "HEALER",
            attendance: "IN",
            spec: "Shadow",
            isCurrentUser: true,
            characterId: "eu-silvermoon-aelrin",
            specId: 258);
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        client.Setup(c => c.SignupAsync("run-1", It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto> { currentSignup }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(
            client,
            characters:
            [
                MakeAppCharacter(
                    activeSpecId: 257,
                    specializations:
                    [
                        new CharacterSpecializationDto(257, "Holy"),
                        new CharacterSpecializationDto(258, "Shadow"),
                    ]),
                MakeAppCharacter(
                    name: "Borin",
                    activeSpecId: 71,
                    specializations:
                    [
                        new CharacterSpecializationDto(71, "Arms"),
                        new CharacterSpecializationDto(72, "Fury"),
                    ]),
            ],
            selectedCharacterId: "eu-silvermoon-borin");

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.changeCharacter"), cut.Markup));

        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.changeCharacter"), StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Borin - Silvermoon", FindSignupSelect(cut, Loc("runs.signup.character")).TextContent));

        FindSignupSelect(cut, Loc("runs.signup.character")).Change("eu-silvermoon-borin");
        FindSignupSelect(cut, Loc("runs.signup.spec")).Change("72");
        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.back"), StringComparison.Ordinal))
            .Click();

        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.changeCharacter"), StringComparison.Ordinal))
            .Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Aelrin - Silvermoon", FindSignupSelect(cut, Loc("runs.signup.character")).TextContent));

        cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.action"), StringComparison.Ordinal))
            .Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.SignupAsync(
                "run-1",
                It.Is<SignupRequest>(r =>
                    r.CharacterId == "eu-silvermoon-aelrin" &&
                    r.DesiredAttendance == "IN" &&
                    r.SpecId == 258),
                It.IsAny<CancellationToken>()),
                Times.Once));
    }

    [Fact]
    public void RunSignupPanel_Uses_Unique_Label_Ids_For_Multiple_Instances()
    {
        var firstSignup = MakeCharacter("Aelrin", isCurrentUser: true, characterId: "eu-silvermoon-aelrin");
        var secondSignup = MakeCharacter("Borin", isCurrentUser: true, characterId: "eu-silvermoon-borin");
        RenderFragment fragment = builder =>
        {
            builder.OpenComponent<RunSignupPanel>(0);
            builder.AddAttribute(1, "RunId", "run-1");
            builder.AddAttribute(2, "CurrentSignup", firstSignup);
            builder.CloseComponent();
            builder.OpenComponent<RunSignupPanel>(3);
            builder.AddAttribute(4, "RunId", "run-2");
            builder.AddAttribute(5, "CurrentSignup", secondSignup);
            builder.CloseComponent();
        };

        var cut = Render(fragment);

        var ids = cut.FindAll("[id]").Select(element => element.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var group in cut.FindAll("[role='radiogroup']"))
        {
            var labelId = group.GetAttribute("aria-labelledby");
            Assert.False(string.IsNullOrWhiteSpace(labelId));
            Assert.Contains(labelId, ids);
        }
    }

    [Fact]
    public void RunsPage_Signup_AutoRefreshes_Characters_When_Cache_NeedsRefresh()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        Services.AddSingleton(client.Object);

        client.SetupSequence(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.NeedsRefresh())
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { MakeAppCharacter() }));

        var battleNet = new Mock<IBattleNetClient>();
        battleNet.Setup(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CharacterDto> { MakeAppCharacter() });
        Services.AddSingleton(battleNet.Object);

        var me = new Mock<IMeClient>();
        me.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MeResponse("bnet-1", null, "eu-silvermoon-aelrin", null, false, "en"));
        Services.AddSingleton(me.Object);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
        {
            client.Verify(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()), Times.Exactly(2));
            battleNet.Verify(c => c.GetCharactersAsync(It.IsAny<CancellationToken>()), Times.Never);
            battleNet.Verify(c => c.RefreshCharactersAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains(Loc("runs.signup.action"), cut.Markup);
            Assert.DoesNotContain(Loc("runs.signup.charactersNeedRefresh"), cut.Markup);
        });
    }

    [Fact]
    public void RunsPage_Signup_Uses_RunScoped_Character_Options()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        var runScopedCharacter = MakeAppCharacter(name: "Guildmain", realm: "silvermoon");
        client.Setup(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Cached(new List<CharacterDto> { runScopedCharacter }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(selectedCharacterId: "eu-silvermoon-guildmain");

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Guildmain", cut.Markup);
            Assert.DoesNotContain("Aelrin", cut.Markup);
            client.Verify(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
        });
    }

    [Fact]
    public void RunsPage_CancelSignup_Requires_Confirmation()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("Aelrin", classId: 5, className: "Priest", role: "HEALER", spec: "Holy", isCurrentUser: true),
            }));
        client.Setup(c => c.CancelSignupAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var dialogModule = JSInterop.SetupModule("./js/dialog.js");
        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.cancel"), cut.Markup));

        var cancelButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.cancel"), StringComparison.Ordinal));
        cancelButton.Click();

        client.Verify(c => c.CancelSignupAsync("run-1", It.IsAny<CancellationToken>()), Times.Never);
        cut.WaitForAssertion(() => dialogModule.VerifyInvoke("showModal"));
        Assert.False(cut.Find("dialog.confirm-dialog").HasAttribute("open"));
        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Loc("runs.signup.cancelConfirmTitle"), cut.Markup);
            Assert.Contains(Loc("runs.signup.cancelConfirmBody"), cut.Markup);
        });

        var dismissButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.cancelConfirmCancel"), StringComparison.Ordinal));
        dismissButton.Click();
        cut.WaitForAssertion(() => dialogModule.VerifyInvoke("close"));
        client.Verify(c => c.CancelSignupAsync("run-1", It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(Loc("runs.signup.cancel"), cut.Markup);

        cancelButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.cancel"), StringComparison.Ordinal));
        cancelButton.Click();

        var confirmButton = cut.FindAll("fluent-button")
            .Single(b => b.TextContent.Contains(Loc("runs.signup.cancelConfirmConfirm"), StringComparison.Ordinal));
        confirmButton.Click();

        cut.WaitForAssertion(() =>
            client.Verify(c => c.CancelSignupAsync("run-1", It.IsAny<CancellationToken>()), Times.Once));
        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(
                cut.FindAll("fluent-button"),
                button => button.TextContent.Trim() == Loc("runs.signup.cancel")));
    }

    [Theory]
    [InlineData("onclose")]
    [InlineData("oncancel")]
    public void ConfirmDialog_Native_Dismiss_Notifies_Cancel_And_Allows_Reopen(string nativeEventName)
    {
        var dialogModule = JSInterop.SetupModule("./js/dialog.js");
        var open = true;
        var cancelCount = 0;
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Open, open)
            .Add(p => p.Title, "Cancel signup")
            .Add(p => p.Body, "Remove this signup?")
            .Add(p => p.ConfirmText, "Remove")
            .Add(p => p.CancelText, "Keep signup")
            .Add(p => p.OnCancel, EventCallback.Factory.Create(this, () =>
            {
                cancelCount++;
                open = false;
            })));

        cut.WaitForAssertion(() => dialogModule.VerifyInvoke("showModal"));

        cut.Find("dialog.confirm-dialog").TriggerEvent(nativeEventName, EventArgs.Empty);

        Assert.False(open);
        Assert.Equal(1, cancelCount);

        cut.Render(parameters => parameters.Add(p => p.Open, open));
        open = true;
        cut.Render(parameters => parameters.Add(p => p.Open, open));

        cut.WaitForAssertion(() => dialogModule.VerifyInvoke("showModal", 2));
    }

    [Fact]
    public void ConfirmDialog_Prefers_Safe_Action_For_Initial_Focus()
    {
        JSInterop.SetupModule("./js/dialog.js");
        var cut = Render<ConfirmDialog>(parameters => parameters
            .Add(p => p.Open, true)
            .Add(p => p.Title, "Cancel signup")
            .Add(p => p.Body, "Remove this signup?")
            .Add(p => p.ConfirmText, "Remove")
            .Add(p => p.CancelText, "Keep signup"));

        var buttons = cut.FindAll("fluent-button").ToList();
        var confirmButton = buttons.Single(button => button.TextContent.Contains("Remove", StringComparison.Ordinal));
        var cancelButton = buttons.Single(button => button.TextContent.Contains("Keep signup", StringComparison.Ordinal));
        var confirmIndex = buttons.IndexOf(confirmButton);
        var cancelIndex = buttons.IndexOf(cancelButton);

        Assert.True(cancelIndex < confirmIndex || cancelButton.HasAttribute("autofocus"));
        Assert.False(confirmButton.HasAttribute("autofocus"));
    }

    [Fact]
    public void RunsPage_Signup_Shows_GuildRankBlocked_Message_When_Options_Are_Forbidden()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary() });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());
        client.Setup(c => c.GetSignupOptionsAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CharactersFetchResult.Forbidden());
        Services.AddSingleton(client.Object);
        WireSignupSupport(selectedCharacterId: null);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.signup.guildRankBlocked"), cut.Markup));
        Assert.DoesNotContain(Loc("runs.signup.noCharacters"), cut.Markup);
        Assert.DoesNotContain(Loc("runs.signup.action"), cut.Markup);
    }

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
    public void RunsPage_Detail_Separates_Summary_Signup_And_Roster_Regions()
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
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("[data-testid='run-roster']")));

        var summary = cut.Find("[data-testid='run-detail-summary']");
        var signup = cut.Find("[data-testid='run-signup-surface']");
        var roster = cut.Find("[data-testid='run-roster']");

        Assert.NotNull(summary.Closest("fluent-card"));
        Assert.Null(roster.Closest("fluent-card"));

        var markup = cut.Markup;
        Assert.True(
            markup.IndexOf("data-testid=\"run-detail-summary\"", StringComparison.Ordinal) <
            markup.IndexOf("data-testid=\"run-signup-surface\"", StringComparison.Ordinal));
        Assert.True(
            markup.IndexOf("data-testid=\"run-signup-surface\"", StringComparison.Ordinal) <
            markup.IndexOf("data-testid=\"run-roster\"", StringComparison.Ordinal));
        Assert.Contains(Loc("runs.attendingSection"), roster.TextContent);
    }

    [Fact]
    public void RunsPage_RunCards_Render_Mobile_Detail_Toggles()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto>
            {
                MakeSummary("run-1"),
                MakeSummary("run-2") with { InstanceName = "Nerub-ar Palace", Difficulty = "MYTHIC" },
            });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("Tankington", classId: 1, className: "Warrior", role: "TANK", spec: "Protection"),
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.NotNull(cut.Find("[data-testid='run-card-run-1']")));

        var selectedToggle = cut.Find("[data-testid='run-mobile-detail-toggle-run-1']");
        var collapsedToggle = cut.Find("[data-testid='run-mobile-detail-toggle-run-2']");

        Assert.Equal("true", selectedToggle.GetAttribute("aria-expanded"));
        Assert.Contains(Loc("runs.hideDetails"), selectedToggle.TextContent);
        Assert.Equal("false", collapsedToggle.GetAttribute("aria-expanded"));
        Assert.Contains(Loc("runs.showDetails"), collapsedToggle.TextContent);
    }

    [Fact]
    public void RunsPage_Mobile_Detail_Toggle_Collapses_Selected_Run()
    {
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { MakeSummary("run-1") });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetailWithRoster(new List<RunCharacterDto>
            {
                MakeCharacter("Tankington", classId: 1, className: "Warrior", role: "TANK", spec: "Protection"),
            }));
        Services.AddSingleton(client.Object);
        WireSignupSupport(client);
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("runs.hideDetails"), cut.Find("[data-testid='run-mobile-detail-toggle-run-1']").TextContent));

        cut.Find("[data-testid='run-mobile-detail-toggle-run-1']").Click();

        cut.WaitForAssertion(() =>
        {
            var toggle = cut.Find("[data-testid='run-mobile-detail-toggle-run-1']");
            Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
            Assert.Contains(Loc("runs.showDetails"), toggle.TextContent);
            Assert.Contains(Loc("runs.selectPrompt"), cut.Find("[data-testid='run-detail-panel']").TextContent);
        });
        Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath);
        client.Verify(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()), Times.Once);
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
        var summary = MakeSummary() with { Difficulty = "MYTHIC", Size = 25 };
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

    [Fact]
    public void RunsPage_RunListItem_FallsBackToAnyDungeonLabel_WhenInstanceNameIsNull()
    {
        // Mythic+ "any dungeon" runs persist InstanceName as null per the
        // wire contract. Without a fallback the list item renders a blank
        // heading, which is what the user reported. Pin the localized label
        // so a locale-key rename or fallback regression is caught here.
        var summary = MakeSummary() with
        {
            InstanceId = null,
            InstanceName = null,
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
        };
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { summary });
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>();

        cut.WaitForAssertion(() =>
        {
            var title = cut.Find(".run-list-item__title");
            Assert.Equal(Loc("runs.anyDungeon"), title.TextContent.Trim());
        });

        // The aria-label template weaves the title into the accessible name
        // too — same fallback must apply so screen-reader users aren't fed
        // an empty string before the date.
        var runButton = cut.Find("button.run-list-item");
        var ariaLabel = runButton.GetAttribute("aria-label") ?? string.Empty;
        Assert.Contains(Loc("runs.anyDungeon"), ariaLabel);
    }

    [Fact]
    public void RunsPage_DetailHeading_FallsBackToAnyDungeonLabel_WhenInstanceNameIsNull()
    {
        var summary = MakeSummary() with
        {
            InstanceId = null,
            InstanceName = null,
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
        };
        var detail = MakeDetail() with
        {
            InstanceId = null,
            InstanceName = null,
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
        };
        var client = new Mock<IRunsClient>();
        client.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RunSummaryDto> { summary });
        client.Setup(c => c.GetAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);
        Services.AddSingleton(client.Object);

        var cut = Render<RunsPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
        {
            var heading = cut.Find(".run-detail-title");
            Assert.Equal(Loc("runs.anyDungeon"), heading.TextContent.Trim());
        });
    }

    // ── CreateRunPage ────────────────────────────────────────────────────────

    private void WireCreateRunServices(
        IReadOnlyList<InstanceDto>? instances = null,
        IReadOnlyList<ExpansionDto>? expansions = null,
        GuildDto? guild = null,
        TaskCompletionSource<IReadOnlyList<InstanceDto>>? instancesPending = null,
        bool guildThrows = false,
        bool siteAdmin = false)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("player#1234");
        if (siteAdmin)
            auth.SetRoles("SiteAdmin");

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
        if (guildThrows)
            guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("guild unavailable"));
        else
            guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(guild ?? MakeGuildDto());
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

    [Fact]
    public void CreateRunPage_Renders_GuildRank_Message_When_Guild_Load_Fails()
    {
        WireCreateRunServices(
            instances: [MakeInstanceFixture()],
            guildThrows: true);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
        Assert.DoesNotContain("Failed to load form data", cut.Markup);
        Assert.DoesNotContain(Loc("createRun.guildOnly"), cut.Markup);
        Assert.Contains(Loc("createRun.visibility.guildDisabledReason"), cut.Markup);
    }

    [Fact]
    public void CreateRunPage_SiteAdmin_DoesNotShowGuildRankMessage_WhenRankCannotCreateRuns()
    {
        WireCreateRunServices(
            instances: [MakeInstanceFixture()],
            guild: MakeGuildDto(canCreateGuildRuns: false),
            siteAdmin: true);

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
        Assert.DoesNotContain(Loc("createRun.visibility.guildDisabledReason"), cut.Markup);
    }

    [Fact]
    public void CreateRunPage_DoesNotRenderExpansionSelector()
    {
        // The create-run form scopes its instance list to the Blizzard
        // "Current Season" tier unconditionally — M+, raids, and non-M+
        // dungeons all come from the current rotation. Pin the absence of
        // an expansion dropdown so a regression that reintroduces cross-
        // expansion selection surfaces here.
        WireCreateRunServices(expansions: new List<ExpansionDto>
        {
            new(505, "The War Within"),
            new(999, CurrentSeasonExpansion),
        });

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
        Assert.Empty(cut.FindAll("#expansion-select"));
    }

    // Regression: HandleSubmit's `_submitting` flag must reset on every exit
    // path, including the success path that navigates away. The original
    // implementation only reset on early-return + catch, leaving the flag
    // stuck `true` after a successful create. Today `Nav.NavigateTo` disposes
    // the page so the stale flag is unobservable, but a future refactor that
    // replaces navigation with an inline success state (toast + reset form)
    // would silently leave the submit button disabled forever. Mirror the
    // EditRunPage `try/finally { _saving = false; }` pattern.
    [Fact]
    public void CreateRunPage_HandleSubmit_Resets_Submitting_Flag_On_Success_Path()
    {
        this.AddAuthorization().SetAuthorized("player#1234");

        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.CreateAsync(It.IsAny<CreateRunRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeDetail());

        var instancesClient = new Mock<IInstancesClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        Services.AddSingleton(instancesClient.Object);

        var expansionsClient = new Mock<IExpansionsClient>();
        expansionsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExpansionDto(505, "The War Within")]);
        Services.AddSingleton(expansionsClient.Object);

        var guildClient = new Mock<IGuildClient>();
        guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeGuildDto());
        Services.AddSingleton(guildClient.Object);

        Services.AddSingleton(runsClient.Object);

        var cut = Render<CreateRunPage>();

        // The default form state after load is M+ + any-dungeon with start
        // time auto-set to next Thursday 20:00. Only the keystone level is
        // required to make CanSubmit return true.
        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.submit"), cut.Markup));
        cut.Find("#keylevel-input").Input("10");

        var submitButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("createRun.submit"), StringComparison.Ordinal));
        submitButton.Click();

        // Verify the success path actually ran.
        cut.WaitForAssertion(() =>
            runsClient.Verify(c => c.CreateAsync(
                It.IsAny<CreateRunRequest>(),
                It.IsAny<CancellationToken>()),
                Times.Once));

        // The flag is private; reflection is the cheapest way to pin the
        // post-condition without coupling to incidental markup. The button's
        // disabled state is a downstream signal but it's also influenced by
        // CanSubmit, so reading the field directly avoids ambiguity.
        var instance = cut.Instance;
        var field = typeof(CreateRunPage).GetField(
            "_submitting",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        cut.WaitForAssertion(() =>
            Assert.False((bool)field!.GetValue(instance)!));
    }

    [Fact]
    public void CreateRunPage_DirtyCancel_ShowsUnsavedDialog_AndLeaveNavigates()
    {
        WireCreateRunServices();
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<CreateRunPage>();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("createRun.cancel"), cut.Markup));
        cut.Find("#keylevel-input").Input("10");

        var cancelButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("createRun.cancel"), StringComparison.Ordinal));
        cancelButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("unsavedChanges.title"), cut.Markup));
        Assert.Equal(NavigationState.Prevented, nav.History.First().State);

        var leaveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("unsavedChanges.leave"), StringComparison.Ordinal));
        leaveButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("/runs", new Uri(nav.Uri).AbsolutePath));
        Assert.Equal(NavigationState.Succeeded, nav.History.First().State);
    }

    // ── EditRunPage ──────────────────────────────────────────────────────────

    private Mock<IRunsClient> WireEditRunServices(
        Mock<IRunsClient>? runsClient = null,
        IReadOnlyList<InstanceDto>? instances = null,
        IReadOnlyList<ExpansionDto>? expansions = null,
        GuildDto? guild = null,
        bool guildThrows = false)
    {
        runsClient ??= new Mock<IRunsClient>();
        var instancesClient = new Mock<IInstancesClient>();
        instancesClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances ?? [MakeInstanceFixture()]);
        var expansionsClient = new Mock<IExpansionsClient>();
        expansionsClient.Setup(c => c.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expansions ?? [new ExpansionDto(505, "The War Within")]);
        var guildClient = new Mock<IGuildClient>();
        if (guildThrows)
            guildClient.Setup(c => c.GetAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("guild unavailable"));
        else
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
        var tcs = new TaskCompletionSource<RunDetailWithEtag?>();
        runsClient.Setup(c => c.GetWithEtagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(tcs.Task);
        WireEditRunServices(runsClient);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        Assert.NotEmpty(cut.FindAll("fluent-progress-ring"));
    }

    [Fact]
    public void EditRunPage_Renders_Form_After_Run_Loads()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        WireEditRunServices(runsClient);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
    }

    [Fact]
    public void EditRunPage_Renders_Public_Form_When_Guild_Load_Fails()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        WireEditRunServices(runsClient, instances: [MakeInstanceFixture()], guildThrows: true);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
        Assert.DoesNotContain("Failed to load run", cut.Markup);
        Assert.DoesNotContain(Loc("createRun.guildOnly"), cut.Markup);
    }

    [Fact]
    public void EditRunPage_DoesNotRenderExpansionSelector()
    {
        // Same rationale as CreateRunPage_DoesNotRenderExpansionSelector:
        // the edit form also scopes to the Current Season tier only.
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        WireEditRunServices(runsClient, expansions: new List<ExpansionDto>
        {
            new(505, "The War Within"),
            new(999, CurrentSeasonExpansion),
        });

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
        Assert.Empty(cut.FindAll("#expansion-select"));
    }

    [Fact]
    public void EditRunPage_Shows_Error_When_Run_Not_Found()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RunDetailWithEtag?)null);
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
        string? capturedIfMatch = null;
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(
                MakeDetail() with { StartTime = OriginalStart, SignupCloseTime = OriginalSignup },
                "\"etag-v1\""));
        runsClient.Setup(c => c.UpdateAsync("run-1", It.IsAny<UpdateRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, UpdateRunRequest, string, CancellationToken>((_, req, ifMatch, _) =>
            {
                captured = req;
                capturedIfMatch = ifMatch;
            })
            .ReturnsAsync((RunDetailWithEtag?)null);
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
        // Save must echo the loaded ETag on If-Match so the server can reject
        // stale updates with 412 instead of silently overwriting.
        Assert.Equal("\"etag-v1\"", capturedIfMatch);
    }

    [Fact]
    public void EditRunPage_Save_Refreshes_Etag_From_Update_Response()
    {
        var ifMatches = new List<string>();
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        runsClient.Setup(c => c.UpdateAsync("run-1", It.IsAny<UpdateRunRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, UpdateRunRequest, string, CancellationToken>((_, _, ifMatch, _) => ifMatches.Add(ifMatch))
            .ReturnsAsync(() =>
            {
                var etag = ifMatches.Count == 1 ? "\"etag-v2\"" : "\"etag-v3\"";
                return new RunDetailWithEtag(MakeDetail(), etag);
            });
        WireEditRunServices(runsClient);

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));

        var saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.saveChanges")));
        saveButton.Click();
        cut.WaitForAssertion(() => Assert.Single(ifMatches));

        saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.saveChanges")));
        saveButton.Click();
        cut.WaitForAssertion(() => Assert.Equal(2, ifMatches.Count));

        Assert.Equal(["\"etag-v1\"", "\"etag-v2\""], ifMatches);
    }

    [Fact]
    public void EditRunPage_DirtyCancel_ShowsUnsavedDialog_AndSuccessfulSaveResetsBaseline()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
        runsClient.Setup(c => c.UpdateAsync("run-1", It.IsAny<UpdateRunRequest>(), "\"etag-v1\"", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail() with { Description = "Updated notes" }, "\"etag-v2\""));
        WireEditRunServices(runsClient);
        JSInterop.SetupModule("./js/unsavedChanges.js");
        JSInterop.SetupModule("./js/dialog.js");
        var nav = Services.GetRequiredService<BunitNavigationManager>();

        var cut = Render<EditRunPage>(p => p.Add(x => x.RunId, "run-1"));

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("editRun.saveChanges"), cut.Markup));
        cut.Find("#description-input").Change("Updated notes");

        var cancelButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.cancel"), StringComparison.Ordinal));
        cancelButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(Loc("unsavedChanges.title"), cut.Markup));
        Assert.Equal(NavigationState.Prevented, nav.History.First().State);

        var stayButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("unsavedChanges.stay"), StringComparison.Ordinal));
        stayButton.Click();

        cut.WaitForAssertion(() =>
            Assert.DoesNotContain(Loc("unsavedChanges.title"), cut.Markup));

        var saveButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.saveChanges"), StringComparison.Ordinal));
        saveButton.Click();
        cut.WaitForAssertion(() =>
            runsClient.Verify(c => c.UpdateAsync(
                "run-1",
                It.IsAny<UpdateRunRequest>(),
                "\"etag-v1\"",
                It.IsAny<CancellationToken>()),
                Times.Once));

        cancelButton = cut.FindAll("fluent-button")
            .First(b => b.TextContent.Contains(Loc("editRun.cancel"), StringComparison.Ordinal));
        cancelButton.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("/runs/run-1", new Uri(nav.Uri).AbsolutePath));
        Assert.Equal(NavigationState.Succeeded, nav.History.First().State);
    }

    // RD-DIALOG-1 (#26): the delete confirmation renders as a native <dialog>
    // so focus trap, Esc dismissal, and focus restoration come from the browser
    // for free. Clicking "Delete run" must open it via the dialog.js interop
    // module rather than flipping a render flag.
    [Fact]
    public void EditRunPage_Delete_Button_Opens_Native_Dialog_Via_Interop()
    {
        var runsClient = new Mock<IRunsClient>();
        runsClient.Setup(c => c.GetWithEtagAsync("run-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RunDetailWithEtag(MakeDetail(), "\"etag-v1\""));
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
