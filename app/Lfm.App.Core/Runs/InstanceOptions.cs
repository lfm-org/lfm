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
                var first = g.First();
                var difficulties = g
                    .Select(i => new DifficultyOption(
                        DifficultyId: i.Difficulty,
                        Size: i.Size,
                        DisplayName: DifficultyLabel.Format(
                            i.Difficulty,
                            i.Size,
                            ActivityKindExtensions.FromCategory(first.Category))))
                    .Where(d => !string.IsNullOrEmpty(d.DifficultyId))
                    .OrderBy(d => CanonicalOrder(d.DifficultyId))
                    .ThenBy(d => d.Size)
                    .ToList();

                return new InstanceOption(
                    InstanceId: first.InstanceNumericId,
                    Name: first.Name,
                    Activity: ActivityKindExtensions.FromCategory(first.Category),
                    ExpansionId: first.ExpansionId,
                    Difficulties: difficulties);
            })
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
}
