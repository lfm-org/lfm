// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Guild;

/// <summary>
/// A single rank permission entry in an UpdateGuildRequest.
/// </summary>
public sealed record UpdateGuildRankPermission(
    int Rank,
    bool CanCreateGuildRuns,
    bool CanSignupGuildRuns,
    bool CanDeleteGuildRuns);

/// <summary>
/// Request body for PATCH /api/guild.
/// Fields mirror the TypeScript <c>parseGuildSettingsInput</c> function in
/// <c>functions/src/lib/guild/settings.ts</c>.
/// </summary>
public sealed record UpdateGuildRequest(
    string? Timezone,
    string? Locale,
    string? Slogan,
    IReadOnlyList<UpdateGuildRankPermission>? RankPermissions);

/// <summary>
/// Validates UpdateGuildRequest.
/// Allowed locales mirror the TypeScript ALLOWED_LOCALES constant in
/// <c>functions/src/lib/guild/settings.ts</c>.
/// Timezone validation is a non-empty string check only; full IANA zone
/// validation is deferred to the service layer (runtime zone DB required).
/// </summary>
public sealed class UpdateGuildRequestValidator : AbstractValidator<UpdateGuildRequest>
{
    private static readonly string[] AllowedLocales =
        ["fi", "en-gb", "de", "fr", "es", "sv", "da", "nb"];

    public UpdateGuildRequestValidator()
    {
        RuleFor(x => x.Timezone)
            .NotEmpty()
            .WithMessage("timezone is required")
            .MaximumLength(64).WithMessage("timezone must be at most 64 characters");

        RuleFor(x => x.Locale)
            .NotEmpty()
            .WithMessage("locale is required")
            .Must(l => l is not null && AllowedLocales.Contains(
                l.Replace('_', '-'), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Invalid locale. Supported: {string.Join(", ", AllowedLocales)}");

        RuleFor(x => x.Slogan)
            .MaximumLength(200).WithMessage("slogan must be at most 200 characters");
    }
}
