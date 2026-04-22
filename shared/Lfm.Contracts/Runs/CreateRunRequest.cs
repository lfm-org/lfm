// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for POST /api/runs.
/// </summary>
public sealed record CreateRunRequest(
    string? StartTime,
    string? SignupCloseTime,
    string? Description,
    string? Visibility,
    int? InstanceId,
    string? InstanceName,
    string? Difficulty,
    int? Size,
    int? KeystoneLevel = null);

public sealed class CreateRunRequestValidator : AbstractValidator<CreateRunRequest>
{
    private static readonly HashSet<string> ValidVisibilities =
        new(StringComparer.Ordinal) { "PUBLIC", "GUILD" };

    internal static readonly HashSet<string> ValidDifficulties =
        new(StringComparer.Ordinal)
        {
            "LFR", "NORMAL", "HEROIC", "MYTHIC", "MYTHIC_KEYSTONE",
        };

    internal const string MythicKeystone = "MYTHIC_KEYSTONE";

    public CreateRunRequestValidator()
    {
        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("startTime is required")
            .MaximumLength(64).WithMessage("startTime must be at most 64 characters");

        RuleFor(x => x.SignupCloseTime)
            .MaximumLength(64).WithMessage("signupCloseTime must be at most 64 characters");

        RuleFor(x => x.Difficulty)
            .NotEmpty().WithMessage("difficulty is required");
        RuleFor(x => x.Difficulty)
            .Must(d => d is null || ValidDifficulties.Contains(d))
            .WithMessage("difficulty must be one of LFR, NORMAL, HEROIC, MYTHIC, MYTHIC_KEYSTONE");

        RuleFor(x => x.Size)
            .NotNull().WithMessage("size is required");
        RuleFor(x => x.Size)
            .InclusiveBetween(1, 40).When(x => x.Size.HasValue)
            .WithMessage("size must be between 1 and 40");

        RuleFor(x => x.Visibility)
            .NotEmpty().WithMessage("visibility is required")
            .Must(v => v is null || ValidVisibilities.Contains(v))
            .WithMessage("visibility must be PUBLIC or GUILD");

        // InstanceId is required unless the run is a Mythic+ session — M+
        // runs may be dungeon-agnostic ("Any dungeon").
        RuleFor(x => x)
            .Must(x => x.InstanceId.HasValue || x.Difficulty == MythicKeystone)
            .WithMessage("instanceId is required for non-Mythic+ runs");

        // KeystoneLevel is only valid on Mythic+ runs, and it becomes required
        // when no specific instance is selected — a dungeon-less M+ run needs
        // the level to mean anything.
        RuleFor(x => x.KeystoneLevel)
            .InclusiveBetween(2, 30).When(x => x.KeystoneLevel.HasValue)
            .WithMessage("keystoneLevel must be between 2 and 30");
        RuleFor(x => x)
            .Must(x => x.Difficulty == MythicKeystone || x.KeystoneLevel is null)
            .WithMessage("keystoneLevel is only valid for Mythic+ runs");
        RuleFor(x => x)
            .Must(x => x.Difficulty != MythicKeystone || x.InstanceId.HasValue || x.KeystoneLevel.HasValue)
            .WithMessage("keystoneLevel is required when no specific dungeon is selected");

        RuleFor(x => x.InstanceName)
            .MaximumLength(128).WithMessage("instanceName must be at most 128 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("description must be at most 2000 characters");
    }
}
