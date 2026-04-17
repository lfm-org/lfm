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
    string? InstanceName);

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
    }
}
