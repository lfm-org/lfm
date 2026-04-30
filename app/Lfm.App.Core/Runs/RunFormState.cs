// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Expansions;

namespace Lfm.App.Runs;

/// <summary>
/// Run-form state and behavior shared by the Create and Edit run pages.
/// Owns the field cluster, derived properties, option-rebuild helpers, and the
/// canonical "current season" resolution. Pages drive lifecycle (load, populate,
/// submit/save/delete) and read state via the public properties.
/// </summary>
public sealed class RunFormState
{
    public const string CurrentSeasonExpansion = "Current Season";

    public IReadOnlyList<InstanceOption> AllInstances { get; private set; } = Array.Empty<InstanceOption>();
    public IReadOnlyList<ExpansionDto> Expansions { get; private set; } = Array.Empty<ExpansionDto>();

    public int ExpansionId { get; set; }
    public ActivityKind Activity { get; private set; } = ActivityKind.Dungeon;
    public int InstanceId { get; set; }
    public string Difficulty { get; private set; } = "MYTHIC_KEYSTONE";
    public int Size { get; private set; } = 5;
    public int? KeystoneLevel { get; set; }
    public bool AnyDungeon { get; set; } = true;
    public DateTime? StartTimeLocal { get; set; }
    public DateTime? SignupCloseLocal { get; set; }
    public bool ShowSignupClose { get; set; }
    public string Visibility { get; set; } = "PUBLIC";
    public string Description { get; set; } = string.Empty;

    public bool CanCreateGuildRuns { get; set; }
    public bool CanShowGuildOption { get; set; }

    public IReadOnlyList<DifficultyOption> DifficultyOptions { get; private set; } = Array.Empty<DifficultyOption>();

    public IEnumerable<InstanceOption> FilteredInstances =>
        AllInstances.Where(o =>
            (o.ExpansionId is null || o.ExpansionId == ExpansionId) &&
            o.Activity == Activity);

    public bool ShowInstanceDropdown =>
        !(Activity == ActivityKind.Dungeon && Difficulty == "MYTHIC_KEYSTONE" && AnyDungeon);

    public bool ShowDifficultyToggle => InstanceId != 0 || !ShowInstanceDropdown;

    public bool CanSubmit
    {
        get
        {
            if (StartTimeLocal is null) return false;
            var isMythicPlus = Activity == ActivityKind.Dungeon && Difficulty == "MYTHIC_KEYSTONE";
            if (isMythicPlus && AnyDungeon)
                return KeystoneLevel is >= 2 and <= 30;
            if (InstanceId == 0) return false;
            return !isMythicPlus || KeystoneLevel is null || KeystoneLevel is >= 2 and <= 30;
        }
    }

    public void LoadOptions(IReadOnlyList<InstanceOption> instances, IReadOnlyList<ExpansionDto> expansions)
    {
        AllInstances = instances;
        Expansions = expansions;
        ExpansionId = ResolveCurrentSeasonId(expansions);
        RefreshDifficultyOptions();
    }

    public void OnActivityChanged(ActivityKind value)
    {
        Activity = value;
        InstanceId = 0;
        AnyDungeon = value == ActivityKind.Dungeon;
        Difficulty = value == ActivityKind.Dungeon ? "MYTHIC_KEYSTONE" : "";
        Size = value == ActivityKind.Dungeon ? 5 : 0;
        KeystoneLevel = null;
        RefreshDifficultyOptions();
    }

    public void OnDungeonScopeChanged(bool anyDungeon)
    {
        AnyDungeon = anyDungeon;
        if (anyDungeon) InstanceId = 0;
        RefreshDifficultyOptions();
    }

    public void OnInstanceChanged(int id)
    {
        InstanceId = id;
        RefreshDifficultyOptions();
        if (DifficultyOptions.Count > 0 && DifficultyOptions.All(o => o.DifficultyId != Difficulty))
        {
            var topMode = FilteredInstances
                .FirstOrDefault(o => o.InstanceId == id)?
                .Difficulties.LastOrDefault();
            if (topMode is not null)
            {
                Difficulty = topMode.DifficultyId;
                Size = topMode.Size;
            }
        }
    }

    public void OnDifficultyChanged(string value)
    {
        Difficulty = value;
        var match = FilteredInstances
            .FirstOrDefault(o => o.InstanceId == InstanceId)?
            .Difficulties.FirstOrDefault(d => d.DifficultyId == value);
        Size = match?.Size ?? 0;
        if (value != "MYTHIC_KEYSTONE") KeystoneLevel = null;
    }

    public void RefreshDifficultyOptions()
    {
        var match = FilteredInstances.FirstOrDefault(o => o.InstanceId == InstanceId);
        DifficultyOptions = match is null
            ? Array.Empty<DifficultyOption>()
            : match.Difficulties.ToList();
    }

    public void SetMode(string difficulty, int size, int? keystoneLevel)
    {
        Difficulty = difficulty;
        Size = size;
        KeystoneLevel = keystoneLevel;
    }

    /// <summary>
    /// Sets all form state atomically for the populate-from-stored-run path.
    /// Unlike <see cref="OnActivityChanged"/>, this does NOT cascade clears —
    /// callers supply every field's intended value.
    /// </summary>
    public void Populate(
        ActivityKind activity,
        int expansionId,
        int instanceId,
        string difficulty,
        int size,
        int? keystoneLevel,
        bool anyDungeon,
        DateTime? startTimeLocal,
        DateTime? signupCloseLocal,
        bool showSignupClose,
        string visibility,
        string description)
    {
        Activity = activity;
        ExpansionId = expansionId;
        InstanceId = instanceId;
        Difficulty = difficulty;
        Size = size;
        KeystoneLevel = keystoneLevel;
        AnyDungeon = anyDungeon;
        StartTimeLocal = startTimeLocal;
        SignupCloseLocal = signupCloseLocal;
        ShowSignupClose = showSignupClose;
        Visibility = visibility;
        Description = description;
        RefreshDifficultyOptions();
    }

    public static int ResolveCurrentSeasonId(IReadOnlyList<ExpansionDto> expansions)
    {
        var current = expansions.FirstOrDefault(e => e.Name == CurrentSeasonExpansion);
        if (current is not null) return current.Id;
        return expansions.Count == 0 ? 0 : expansions.MaxBy(e => e.Id)!.Id;
    }
}
