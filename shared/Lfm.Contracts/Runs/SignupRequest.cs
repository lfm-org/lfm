// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for POST /api/runs/{id}/signup.
/// Mirrors the Zod <c>signupSchema</c> in
/// <c>functions/src/functions/runs-signup.ts</c>.
/// </summary>
public sealed record SignupRequest(
    string? CharacterId,
    string? DesiredAttendance,
    int? SpecId);

public sealed class SignupRequestValidator : AbstractValidator<SignupRequest>
{
    private static readonly HashSet<string> ValidAttendances =
        new(StringComparer.Ordinal) { "IN", "OUT", "BENCH", "LATE", "AWAY" };

    public SignupRequestValidator()
    {
        RuleFor(x => x.CharacterId)
            .NotEmpty().WithMessage("characterId is required");

        RuleFor(x => x.DesiredAttendance)
            .NotEmpty().WithMessage("desiredAttendance is required")
            .Must(v => v is not null && ValidAttendances.Contains(v))
            .WithMessage("desiredAttendance must be one of: IN, OUT, BENCH, LATE, AWAY");
    }
}
