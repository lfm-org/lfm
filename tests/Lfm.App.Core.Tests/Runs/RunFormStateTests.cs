// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Lfm.Contracts.Expansions;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunFormStateTests
{
    private static readonly IReadOnlyList<ExpansionDto> Expansions = new[]
    {
        new ExpansionDto(Id: 11, Name: "The War Within"),
        new ExpansionDto(Id: 14, Name: "Current Season"),
    };

    private static readonly IReadOnlyList<InstanceOption> Instances = new[]
    {
        new InstanceOption(
            InstanceId: 1023,
            Name: "Some Dungeon",
            Activity: ActivityKind.Dungeon,
            ExpansionId: 14,
            Difficulties: new[]
            {
                new DifficultyOption("MYTHIC_KEYSTONE", 5, "Mythic+ (5)"),
                new DifficultyOption("HEROIC", 5, "Heroic (5)"),
            }),
        new InstanceOption(
            InstanceId: 2055,
            Name: "Some Raid",
            Activity: ActivityKind.Raid,
            ExpansionId: 14,
            Difficulties: new[]
            {
                new DifficultyOption("MYTHIC", 20, "Mythic (20)"),
                new DifficultyOption("HEROIC", 20, "Heroic (20)"),
                new DifficultyOption("NORMAL", 20, "Normal (20)"),
            }),
    };

    [Fact]
    public void OnActivityChanged_ToRaid_ClearsInstanceAndKeystone()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnActivityChanged(ActivityKind.Dungeon);
        state.InstanceId = 1023;
        state.KeystoneLevel = 12;
        state.OnActivityChanged(ActivityKind.Raid);

        Assert.Equal(ActivityKind.Raid, state.Activity);
        Assert.Equal(0, state.InstanceId);
        Assert.Null(state.KeystoneLevel);
        Assert.Equal("", state.Difficulty);
    }

    [Fact]
    public void OnDifficultyChanged_NonMythicPlus_ClearsKeystone()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnActivityChanged(ActivityKind.Dungeon);
        state.InstanceId = 1023;
        state.KeystoneLevel = 12;
        state.OnDifficultyChanged("HEROIC");

        Assert.Equal("HEROIC", state.Difficulty);
        Assert.Null(state.KeystoneLevel);
    }

    [Fact]
    public void CanSubmit_AnyDungeon_RequiresKeystoneInRange()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnActivityChanged(ActivityKind.Dungeon);
        state.AnyDungeon = true;
        state.StartTimeLocal = DateTime.Now.AddDays(1);

        state.KeystoneLevel = null;
        Assert.False(state.CanSubmit);

        state.KeystoneLevel = 1;
        Assert.False(state.CanSubmit);

        state.KeystoneLevel = 12;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 31;
        Assert.False(state.CanSubmit);
    }

    [Fact]
    public void ResolveCurrentSeason_FallsBackToHighestId()
    {
        var noCanonical = new[] { new ExpansionDto(Id: 7, Name: "Cataclysm"), new ExpansionDto(Id: 11, Name: "TWW") };
        Assert.Equal(11, RunFormState.ResolveCurrentSeasonId(noCanonical));
    }

    [Fact]
    public void ResolveCurrentSeason_PrefersCanonicalMatch()
    {
        Assert.Equal(14, RunFormState.ResolveCurrentSeasonId(Expansions));
    }

    [Fact]
    public void OnInstanceChanged_PicksTopModeDifficulty_WhenCurrentUnavailable()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);

        // Start on the dungeon, on Mythic Keystone difficulty.
        state.OnActivityChanged(ActivityKind.Dungeon);
        state.OnInstanceChanged(1023); // dungeon "Some Dungeon" — has MYTHIC_KEYSTONE + HEROIC

        // Switch to a raid via Activity. After Activity → Raid, AnyDungeon is false,
        // Difficulty is "" (empty) per OnActivityChanged, and InstanceId is 0.
        state.OnActivityChanged(ActivityKind.Raid);
        Assert.Equal("", state.Difficulty);

        // Now select a raid instance whose difficulty options do NOT contain "" —
        // the cascade rule should pick the top-of-list (last) DifficultyOption.
        // Test data: "Some Raid" (id 2055) lists MYTHIC, HEROIC, NORMAL in that
        // order, so LastOrDefault() = NORMAL with size 20.
        state.OnInstanceChanged(2055);

        Assert.Equal("NORMAL", state.Difficulty);
        Assert.Equal(20, state.Size);
    }

    [Fact]
    public void Populate_SetsAllFields_WithoutCascading()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);

        state.Populate(
            activity: ActivityKind.Raid,
            expansionId: 14,
            instanceId: 2055,
            difficulty: "MYTHIC",
            size: 20,
            keystoneLevel: null,
            anyDungeon: false,
            startTimeLocal: new DateTime(2026, 5, 1, 20, 0, 0),
            signupCloseLocal: null,
            showSignupClose: false,
            visibility: "PUBLIC",
            description: "test");

        Assert.Equal(ActivityKind.Raid, state.Activity);
        Assert.Equal(2055, state.InstanceId);
        Assert.Equal("MYTHIC", state.Difficulty);
        Assert.Equal(20, state.Size);
        Assert.Null(state.KeystoneLevel);
        Assert.False(state.AnyDungeon);
        Assert.Equal("test", state.Description);
        // The Mythic raid difficulty options for instance 2055 must be loaded
        // (RefreshDifficultyOptions ran).
        Assert.Contains(state.DifficultyOptions, d => d.DifficultyId == "MYTHIC");
    }
}
