// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Me;

/// <summary>
/// Request body for PATCH /api/me.
/// Fields mirror the TypeScript handler at functions/src/functions/me-update.ts.
/// </summary>
public sealed record UpdateMeRequest(string? Locale);

/// <summary>
/// Validates UpdateMeRequest. Supported locales mirror the TS SUPPORTED_LOCALES constant.
/// </summary>
public sealed class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    private static readonly string[] SupportedLocales = ["en", "fi"];

    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.Locale)
            .NotEmpty()
            .WithMessage("locale is required")
            .Must(l => l is not null && SupportedLocales.Contains(l))
            .WithMessage($"Invalid locale. Supported: {string.Join(", ", SupportedLocales)}");
    }
}
