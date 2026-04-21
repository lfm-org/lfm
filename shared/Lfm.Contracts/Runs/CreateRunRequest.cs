// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for POST /api/runs.
/// Mirrors the Zod <c>createRunSchema</c> in
/// <c>functions/src/functions/runs-create.ts</c>.
/// </summary>
public sealed record CreateRunRequest(
    string? StartTime,
    string? SignupCloseTime,
    string? Description,
    string? ModeKey,
    string? Visibility,
    int? InstanceId,
    string? InstanceName);

public sealed class CreateRunRequestValidator : AbstractValidator<CreateRunRequest>
{
    private static readonly HashSet<string> ValidVisibilities =
        new(StringComparer.Ordinal) { "PUBLIC", "GUILD" };

    public CreateRunRequestValidator()
    {
        RuleFor(x => x.StartTime)
            .NotEmpty().WithMessage("startTime is required")
            .MaximumLength(64).WithMessage("startTime must be at most 64 characters");

        RuleFor(x => x.SignupCloseTime)
            .MaximumLength(64).WithMessage("signupCloseTime must be at most 64 characters");

        RuleFor(x => x.ModeKey)
            .NotEmpty().WithMessage("modeKey is required")
            .MaximumLength(64).WithMessage("modeKey must be at most 64 characters");

        RuleFor(x => x.Visibility)
            .NotEmpty().WithMessage("visibility is required")
            .Must(v => v is not null && ValidVisibilities.Contains(v))
            .WithMessage("visibility must be PUBLIC or GUILD");

        RuleFor(x => x.InstanceId)
            .NotNull().WithMessage("instanceId is required");

        RuleFor(x => x.InstanceName)
            .MaximumLength(128).WithMessage("instanceName must be at most 128 characters");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("description must be at most 2000 characters");
    }
}
