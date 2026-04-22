// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.App.Runs;
using Lfm.Contracts.Instances;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class InstanceOptionsTests
{
    private static InstanceDto Row(
        int numericId,
        string name,
        string difficulty,
        int size,
        string? category = "RAID",
        int? expansionId = 505) =>
        new(
            Id: $"{numericId}:{difficulty}:{size}",
            InstanceNumericId: numericId,
            Name: name,
            ModeKey: $"{difficulty}:{size}",
            Expansion: "The War Within",
            Category: category,
            ExpansionId: expansionId,
            Difficulty: difficulty,
            Size: size);

    [Fact]
    public void Groups_multiple_mode_rows_into_one_option_per_instance()
    {
        var flat = new List<InstanceDto>
        {
            Row(1200, "Liberation", "NORMAL", 30),
            Row(1200, "Liberation", "HEROIC", 30),
            Row(1200, "Liberation", "MYTHIC", 20),
        };
        var options = InstanceOptions.Build(flat);

        var opt = Assert.Single(options);
        Assert.Equal(1200, opt.InstanceId);
        Assert.Equal("Liberation", opt.Name);
        Assert.Equal(ActivityKind.Raid, opt.Activity);
        Assert.Equal(3, opt.Difficulties.Count);
    }

    [Fact]
    public void Orders_difficulties_canonically_LFR_first_MythicPlus_last()
    {
        var flat = new List<InstanceDto>
        {
            Row(1200, "Liberation", "MYTHIC", 20),
            Row(1200, "Liberation", "LFR", 30),
            Row(1200, "Liberation", "HEROIC", 30),
            Row(1200, "Liberation", "NORMAL", 30),
        };
        var opt = Assert.Single(InstanceOptions.Build(flat));
        Assert.Equal(
            new[] { "LFR", "NORMAL", "HEROIC", "MYTHIC" },
            opt.Difficulties.Select(d => d.DifficultyId).ToArray());
    }

    [Fact]
    public void Classifies_activity_from_category()
    {
        var raid = Row(1, "R", "HEROIC", 25, category: "RAID");
        var dungeon = Row(2, "D", "MYTHIC_KEYSTONE", 5, category: "DUNGEON");
        var other = Row(3, "O", "NORMAL", 0, category: null);

        var opts = InstanceOptions.Build([raid, dungeon, other]);
        Assert.Equal(ActivityKind.Raid, opts.Single(o => o.InstanceId == 1).Activity);
        Assert.Equal(ActivityKind.Dungeon, opts.Single(o => o.InstanceId == 2).Activity);
        Assert.Equal(ActivityKind.Other, opts.Single(o => o.InstanceId == 3).Activity);
    }

    [Fact]
    public void Orders_instances_alphabetically_case_insensitively()
    {
        var flat = new List<InstanceDto>
        {
            Row(3, "zebra", "HEROIC", 25),
            Row(1, "Ara-Kara", "MYTHIC_KEYSTONE", 5, category: "DUNGEON"),
            Row(2, "Mists", "MYTHIC_KEYSTONE", 5, category: "DUNGEON"),
        };
        var names = InstanceOptions.Build(flat).Select(o => o.Name).ToArray();
        Assert.Equal(new[] { "Ara-Kara", "Mists", "zebra" }, names);
    }

    [Fact]
    public void Drops_empty_difficulty_entries()
    {
        // A legacy manifest row where the mode couldn't be split shows up
        // with Difficulty="". Don't surface it as a difficulty option.
        var flat = new List<InstanceDto>
        {
            Row(1200, "Liberation", "HEROIC", 25),
            Row(1200, "Liberation", "", 0),
        };
        var opt = Assert.Single(InstanceOptions.Build(flat));
        Assert.Single(opt.Difficulties);
        Assert.Equal("HEROIC", opt.Difficulties[0].DifficultyId);
    }

    [Fact]
    public void Carries_ExpansionId_through_from_any_row()
    {
        var flat = new List<InstanceDto> { Row(1200, "L", "HEROIC", 25, expansionId: 505) };
        var opt = Assert.Single(InstanceOptions.Build(flat));
        Assert.Equal(505, opt.ExpansionId);
    }
}
