// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Helpers;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests.Helpers;

public class RunModeResolverTests
{
    // ── Resolve ─────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_returns_structured_fields_when_both_present()
    {
        var (difficulty, size) = RunModeResolver.Resolve("HEROIC", 25, modeKey: "NORMAL:10");
        Assert.Equal("HEROIC", difficulty);
        Assert.Equal(25, size);
    }

    [Fact]
    public void Resolve_falls_back_to_ModeKey_when_structured_fields_empty()
    {
        var (difficulty, size) = RunModeResolver.Resolve(null, 0, "MYTHIC:20");
        Assert.Equal("MYTHIC", difficulty);
        Assert.Equal(20, size);
    }

    [Fact]
    public void Resolve_fills_only_missing_piece_when_other_present()
    {
        // Size missing, Difficulty present → use parsed Size but retain typed Difficulty.
        var (d1, s1) = RunModeResolver.Resolve("HEROIC", 0, "MYTHIC:20");
        Assert.Equal("HEROIC", d1);
        Assert.Equal(20, s1);

        // Difficulty missing, Size present → use parsed Difficulty but retain typed Size.
        var (d2, s2) = RunModeResolver.Resolve(null, 25, "NORMAL:10");
        Assert.Equal("NORMAL", d2);
        Assert.Equal(25, s2);
    }

    [Fact]
    public void Resolve_returns_defaults_when_everything_missing()
    {
        var (difficulty, size) = RunModeResolver.Resolve(null, 0, null);
        Assert.Equal("", difficulty);
        Assert.Equal(0, size);
    }

    // ── EnsurePopulated ─────────────────────────────────────────────────────

    private static RunDocument MakeDoc(string modeKey, string difficulty, int size) =>
        new(
            Id: "run-1",
            StartTime: "2026-05-01T20:00:00Z",
            SignupCloseTime: "",
            Description: "",
            ModeKey: modeKey,
            Visibility: "PUBLIC",
            CreatorGuild: "",
            CreatorGuildId: null,
            InstanceId: 1200,
            InstanceName: "Undermine",
            CreatorBattleNetId: "bnet-1",
            CreatedAt: "2026-04-01T00:00:00Z",
            Ttl: 604800,
            RunCharacters: [],
            Difficulty: difficulty,
            Size: size);

    [Fact]
    public void EnsurePopulated_returns_same_instance_when_fields_already_set()
    {
        var doc = MakeDoc(modeKey: "HEROIC:25", difficulty: "HEROIC", size: 25);
        var result = RunModeResolver.EnsurePopulated(doc);
        Assert.Same(doc, result);
    }

    [Fact]
    public void EnsurePopulated_backfills_from_ModeKey_when_fields_empty()
    {
        var legacy = MakeDoc(modeKey: "MYTHIC:20", difficulty: "", size: 0);
        var result = RunModeResolver.EnsurePopulated(legacy);
        Assert.NotSame(legacy, result);
        Assert.Equal("MYTHIC", result.Difficulty);
        Assert.Equal(20, result.Size);
        Assert.Equal("MYTHIC:20", result.ModeKey); // legacy field untouched
    }
}
