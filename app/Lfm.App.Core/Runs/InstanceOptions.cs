// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Instances;

namespace Lfm.App.Runs;

/// <summary>
/// One difficulty option for a given instance — e.g. ("HEROIC", 25, "Heroic (25)").
/// </summary>
public sealed record DifficultyOption(string DifficultyId, int Size, string DisplayName);

/// <summary>
/// Groups the flat per-(instance, mode) <see cref="InstanceDto"/> rows emitted
/// by the API into one option per instance, each carrying its available
/// difficulty modes. Consumed by the create-run page to drive the activity /
/// instance / difficulty selectors.
/// </summary>
public sealed record InstanceOption(
    int InstanceId,
    string Name,
    ActivityKind Activity,
    int? ExpansionId,
    IReadOnlyList<DifficultyOption> Difficulties);

public static class InstanceOptions
{
    /// <summary>
    /// Group the flat <see cref="InstanceDto"/> list into one option per
    /// instance. Difficulties are ordered by canonical WoW difficulty order
    /// (LFR → Normal → Heroic → Mythic → M+); unknown difficulties trail.
    /// </summary>
    public static IReadOnlyList<InstanceOption> Build(IReadOnlyList<InstanceDto> flat)
    {
        return flat
            .GroupBy(i => i.InstanceNumericId)
            .Select(g =>
            {
                var rows = g.ToList();
                var first = g.First();
                var activity = ResolveActivity(rows);
                var difficulties = rows
                    .Where(i => !string.IsNullOrEmpty(i.Difficulty))
                    .Select(i => new DifficultyOption(
                        DifficultyId: i.Difficulty,
                        Size: i.Size,
                        DisplayName: DifficultyLabel.Format(i.Difficulty, i.Size, activity)))
                    .OrderBy(d => CanonicalOrder(d.DifficultyId))
                    .ThenBy(d => d.Size)
                    .ToList();

                return new InstanceOption(
                    InstanceId: first.InstanceNumericId,
                    Name: first.Name,
                    Activity: activity,
                    ExpansionId: first.ExpansionId,
                    Difficulties: difficulties);
            })
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ActivityKind ResolveActivity(IReadOnlyList<InstanceDto> rows)
    {
        var explicitActivity = ActivityKindExtensions.FromCategory(rows[0].Category);
        if (explicitActivity != ActivityKind.Other) return explicitActivity;

        if (rows.Any(r => r.Difficulty == "MYTHIC_KEYSTONE")) return ActivityKind.Dungeon;
        if (rows.Any(r => r.Difficulty == "LFR" || r.Size > 5)) return ActivityKind.Raid;

        return rows.Any(r => r.Size == 5 && IsDungeonDifficulty(r.Difficulty))
            ? ActivityKind.Dungeon
            : ActivityKind.Other;
    }

    private static int CanonicalOrder(string difficulty) => difficulty switch
    {
        "LFR" => 0,
        "NORMAL" => 1,
        "HEROIC" => 2,
        "MYTHIC" => 3,
        "MYTHIC_KEYSTONE" => 4,
        _ => 99,
    };

    private static bool IsDungeonDifficulty(string difficulty) => difficulty switch
    {
        "NORMAL" or "HEROIC" or "MYTHIC" => true,
        _ => false,
    };
}
