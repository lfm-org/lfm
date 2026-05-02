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
    public void Defaults_match_create_form_initial_state()
    {
        var state = new RunFormState();

        Assert.Equal(ActivityKind.Dungeon, state.Activity);
        Assert.Equal("MYTHIC_KEYSTONE", state.Difficulty);
        Assert.Equal(5, state.Size);
        Assert.True(state.AnyDungeon);
        Assert.Equal("GUILD", state.Visibility);
        Assert.Equal("", state.Description);
    }

    [Fact]
    public void FilteredInstances_include_global_options_and_match_activity_and_expansion()
    {
        var globalDungeon = new InstanceOption(
            InstanceId: 1001,
            Name: "Global Dungeon",
            Activity: ActivityKind.Dungeon,
            ExpansionId: null,
            Difficulties: [new DifficultyOption("MYTHIC_KEYSTONE", 5, "Mythic+ (5)")]);
        var oldDungeon = new InstanceOption(
            InstanceId: 1002,
            Name: "Old Dungeon",
            Activity: ActivityKind.Dungeon,
            ExpansionId: 11,
            Difficulties: [new DifficultyOption("MYTHIC_KEYSTONE", 5, "Mythic+ (5)")]);
        var currentRaid = new InstanceOption(
            InstanceId: 1003,
            Name: "Current Raid",
            Activity: ActivityKind.Raid,
            ExpansionId: 14,
            Difficulties: [new DifficultyOption("NORMAL", 10, "Normal (10)")]);
        var state = new RunFormState();
        state.LoadOptions([globalDungeon, oldDungeon, currentRaid], Expansions);

        var dungeonIds = state.FilteredInstances.Select(i => i.InstanceId).ToArray();

        Assert.Equal(new[] { 1001 }, dungeonIds);

        state.OnActivityChanged(ActivityKind.Raid);
        Assert.Equal(new[] { 1003 }, state.FilteredInstances.Select(i => i.InstanceId).ToArray());
    }

    [Fact]
    public void Visibility_properties_follow_activity_scope_and_instance_selection()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);

        Assert.False(state.ShowInstanceDropdown);
        Assert.True(state.ShowDifficultyToggle);

        state.OnDungeonScopeChanged(false);

        Assert.True(state.ShowInstanceDropdown);
        Assert.False(state.ShowDifficultyToggle);

        state.OnInstanceChanged(1023);

        Assert.True(state.ShowInstanceDropdown);
        Assert.True(state.ShowDifficultyToggle);

        state.OnActivityChanged(ActivityKind.Raid);

        Assert.True(state.ShowInstanceDropdown);
        Assert.False(state.ShowDifficultyToggle);

        state.OnActivityChanged(ActivityKind.Dungeon);
        state.SetMode("HEROIC", 5, null);

        Assert.True(state.ShowInstanceDropdown);
    }

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
        Assert.Equal(0, state.Size);
        Assert.Empty(state.DifficultyOptions);
    }

    [Fact]
    public void OnActivityChanged_ToDungeon_RestoresMythicPlusDefaults()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnActivityChanged(ActivityKind.Raid);
        state.InstanceId = 2055;
        state.OnDifficultyChanged("HEROIC");

        state.OnActivityChanged(ActivityKind.Dungeon);

        Assert.Equal(ActivityKind.Dungeon, state.Activity);
        Assert.Equal(0, state.InstanceId);
        Assert.True(state.AnyDungeon);
        Assert.Equal("MYTHIC_KEYSTONE", state.Difficulty);
        Assert.Equal(5, state.Size);
        Assert.Null(state.KeystoneLevel);
        Assert.Empty(state.DifficultyOptions);
    }

    [Fact]
    public void OnDungeonScopeChanged_clears_instance_only_for_any_dungeon()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnDungeonScopeChanged(false);
        state.OnInstanceChanged(1023);

        state.OnDungeonScopeChanged(false);

        Assert.Equal(1023, state.InstanceId);
        Assert.NotEmpty(state.DifficultyOptions);

        state.OnDungeonScopeChanged(true);

        Assert.Equal(0, state.InstanceId);
        Assert.Empty(state.DifficultyOptions);
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
    public void OnDifficultyChanged_MatchingMode_updates_size_and_preserves_mythic_plus_keystone()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnDungeonScopeChanged(false);
        state.OnInstanceChanged(1023);
        state.KeystoneLevel = 12;

        state.OnDifficultyChanged("HEROIC");

        Assert.Equal("HEROIC", state.Difficulty);
        Assert.Equal(5, state.Size);
        Assert.Null(state.KeystoneLevel);

        state.KeystoneLevel = 10;
        state.OnDifficultyChanged("MYTHIC_KEYSTONE");

        Assert.Equal("MYTHIC_KEYSTONE", state.Difficulty);
        Assert.Equal(5, state.Size);
        Assert.Equal(10, state.KeystoneLevel);
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

        state.KeystoneLevel = 2;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 12;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 30;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 31;
        Assert.False(state.CanSubmit);
    }

    [Fact]
    public void CanSubmit_SpecificMythicPlus_requires_start_time_instance_and_valid_optional_keystone()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnDungeonScopeChanged(false);
        state.StartTimeLocal = null;
        state.OnInstanceChanged(1023);

        Assert.False(state.CanSubmit);

        state.StartTimeLocal = DateTime.Now.AddDays(1);
        state.InstanceId = 0;
        Assert.False(state.CanSubmit);

        state.OnInstanceChanged(1023);

        state.KeystoneLevel = null;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 1;
        Assert.False(state.CanSubmit);

        state.KeystoneLevel = 2;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 30;
        Assert.True(state.CanSubmit);

        state.KeystoneLevel = 31;
        Assert.False(state.CanSubmit);
    }

    [Fact]
    public void CanSubmit_NonMythicPlus_requires_start_time_and_instance()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnActivityChanged(ActivityKind.Raid);
        state.OnInstanceChanged(2055);
        state.OnDifficultyChanged("NORMAL");

        Assert.False(state.CanSubmit);

        state.StartTimeLocal = DateTime.Now.AddDays(1);

        Assert.True(state.CanSubmit);
    }

    [Fact]
    public void CanSubmit_DungeonHeroicWithInstance_does_not_require_keystone()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnInstanceChanged(1023);
        state.OnDifficultyChanged("HEROIC");
        state.StartTimeLocal = DateTime.Now.AddDays(1);

        Assert.True(state.CanSubmit);
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
    public void ResolveCurrentSeason_ReturnsZeroWhenNoExpansionsExist()
    {
        Assert.Equal(0, RunFormState.ResolveCurrentSeasonId([]));
    }

    [Fact]
    public void LoadOptions_refreshes_difficulty_options_for_existing_instance()
    {
        var state = new RunFormState
        {
            InstanceId = 1023,
        };

        state.LoadOptions(Instances, Expansions);

        Assert.Contains(state.DifficultyOptions, d => d.DifficultyId == "MYTHIC_KEYSTONE");
    }

    [Fact]
    public void OnInstanceChanged_PreservesCurrentDifficulty_WhenAvailable()
    {
        var state = new RunFormState();
        state.LoadOptions(Instances, Expansions);
        state.OnDungeonScopeChanged(false);

        state.OnInstanceChanged(1023);

        Assert.Equal("MYTHIC_KEYSTONE", state.Difficulty);
        Assert.Equal(5, state.Size);
    }

    [Fact]
    public void OnDifficultyChanged_without_instance_or_match_sets_size_to_zero()
    {
        var customInstances = new[]
        {
            new InstanceOption(
                InstanceId: 3001,
                Name: "Flexible Dungeon",
                Activity: ActivityKind.Dungeon,
                ExpansionId: 14,
                Difficulties: new[]
                {
                    new DifficultyOption("SMALL", 5, "Small (5)"),
                    new DifficultyOption("LARGE", 10, "Large (10)"),
                }),
        };
        var state = new RunFormState();
        state.LoadOptions(customInstances, Expansions);

        state.OnDifficultyChanged("SMALL");

        Assert.Equal(0, state.Size);

        state.OnInstanceChanged(3001);
        state.OnDifficultyChanged("UNKNOWN");

        Assert.Equal("UNKNOWN", state.Difficulty);
        Assert.Equal(0, state.Size);

        state.OnDifficultyChanged("SMALL");

        Assert.Equal(5, state.Size);
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
            visibility: "GUILD",
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

    [Fact]
    public void SetMode_replaces_difficulty_size_and_keystone()
    {
        var state = new RunFormState();

        state.SetMode("HEROIC", 10, 7);

        Assert.Equal("HEROIC", state.Difficulty);
        Assert.Equal(10, state.Size);
        Assert.Equal(7, state.KeystoneLevel);
    }
}
