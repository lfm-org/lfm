// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for PATCH/PUT /api/runs/{id}.
/// All fields are optional; only supplied fields are applied to the existing run.
/// Mirrors <c>UpdateRunBody</c> in <c>functions/src/functions/runs-update.ts</c>.
/// </summary>
public sealed record UpdateRunRequest(
    string? StartTime,
    string? SignupCloseTime,
    string? Description,
    string? ModeKey,
    string? Visibility,
    int? InstanceId,
    string? InstanceName,
    string? Difficulty = null,
    int? Size = null,
    int? KeystoneLevel = null);

public sealed class UpdateRunRequestValidator : AbstractValidator<UpdateRunRequest>
{
    private static readonly HashSet<string> ValidVisibilities =
        new(StringComparer.Ordinal) { "PUBLIC", "GUILD" };

    public UpdateRunRequestValidator()
    {
        // All fields are optional on update, but if provided they must be valid.
        RuleFor(x => x.Visibility)
            .Must(v => v is null || ValidVisibilities.Contains(v))
            .WithMessage("visibility must be PUBLIC or GUILD");

        RuleFor(x => x.StartTime)
            .MaximumLength(64).WithMessage("startTime must be at most 64 characters");

        RuleFor(x => x.SignupCloseTime)
            .MaximumLength(64).WithMessage("signupCloseTime must be at most 64 characters");

        RuleFor(x => x.ModeKey)
            .MaximumLength(64).WithMessage("modeKey must be at most 64 characters");

        RuleFor(x => x.Difficulty)
            .Must(d => d is null || CreateRunRequestValidator.ValidDifficulties.Contains(d))
            .WithMessage("difficulty must be one of LFR, NORMAL, HEROIC, MYTHIC, MYTHIC_KEYSTONE");

        RuleFor(x => x.Size)
            .InclusiveBetween(1, 40).When(x => x.Size.HasValue)
            .WithMessage("size must be between 1 and 40");

        RuleFor(x => x.KeystoneLevel)
            .InclusiveBetween(2, 30).When(x => x.KeystoneLevel.HasValue)
            .WithMessage("keystoneLevel must be between 2 and 30");

        RuleFor(x => x.InstanceName)
            .MaximumLength(128).WithMessage("instanceName must be at most 128 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("description must be at most 2000 characters");
    }
}
