// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using System.Globalization;
using FluentValidation;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Me;
using Lfm.Contracts.Raiders;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Validation;

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
        RuleFor(x => x.StartTime)
            .Must(RunRequestTimeRules.IsValidDateTimeOrEmpty)
            .WithMessage("startTime must be a valid date/time");

        RuleFor(x => x.SignupCloseTime)
            .MaximumLength(64).WithMessage("signupCloseTime must be at most 64 characters");
        RuleFor(x => x.SignupCloseTime)
            .Must(RunRequestTimeRules.IsValidDateTimeOrEmpty)
            .WithMessage("signupCloseTime must be a valid date/time");
        RuleFor(x => x)
            .Must(x => RunRequestTimeRules.SignupCloseTimeIsBeforeStartTime(x.SignupCloseTime, x.StartTime))
            .WithMessage("signupCloseTime must be before startTime");

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

        RuleFor(x => x)
            .Must(x => x.InstanceId.HasValue || x.Difficulty == MythicKeystone)
            .WithMessage("instanceId is required for non-Mythic+ runs");

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

public sealed class UpdateRunRequestValidator : AbstractValidator<UpdateRunRequest>
{
    private static readonly HashSet<string> ValidVisibilities =
        new(StringComparer.Ordinal) { "PUBLIC", "GUILD" };

    public UpdateRunRequestValidator()
    {
        RuleFor(x => x.Visibility)
            .Must(v => v is null || ValidVisibilities.Contains(v))
            .WithMessage("visibility must be PUBLIC or GUILD");

        RuleFor(x => x.StartTime)
            .MaximumLength(64).WithMessage("startTime must be at most 64 characters");
        RuleFor(x => x.StartTime)
            .Must(RunRequestTimeRules.IsValidDateTimeOrMissing)
            .WithMessage("startTime must be a valid date/time");

        RuleFor(x => x.SignupCloseTime)
            .MaximumLength(64).WithMessage("signupCloseTime must be at most 64 characters");
        RuleFor(x => x.SignupCloseTime)
            .Must(RunRequestTimeRules.IsValidDateTimeOrEmpty)
            .WithMessage("signupCloseTime must be a valid date/time");
        RuleFor(x => x)
            .Must(x => RunRequestTimeRules.SignupCloseTimeIsBeforeStartTime(x.SignupCloseTime, x.StartTime))
            .WithMessage("signupCloseTime must be before startTime");

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

public partial class AddCharacterRequestValidator : AbstractValidator<AddCharacterRequest>
{
    private static readonly HashSet<string> ValidRegions = ["eu", "us", "kr", "tw", "cn"];

    [GeneratedRegex("^[a-z0-9-]+$")]
    private static partial Regex RealmPattern();

    [GeneratedRegex(@"^[a-zA-Z\u00C0-\u00FF]+$")]
    private static partial Regex NamePattern();

    public AddCharacterRequestValidator()
    {
        RuleFor(x => x.Region)
            .NotEmpty().WithMessage("region is required")
            .Must(r => r is not null && ValidRegions.Contains(r))
            .WithMessage("region must be one of: eu, us, kr, tw, cn");

        RuleFor(x => x.Realm)
            .NotEmpty().WithMessage("realm is required")
            .MaximumLength(64).WithMessage("realm must be at most 64 characters")
            .Must(r => r is not null && RealmPattern().IsMatch(r))
            .WithMessage("realm must be a valid slug (lowercase, alphanumeric, hyphens)");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("name is required")
            .MinimumLength(2).WithMessage("name must be at least 2 characters")
            .MaximumLength(12).WithMessage("name must be at most 12 characters")
            .Must(n => n is not null && NamePattern().IsMatch(n))
            .WithMessage("name must contain only letters");
    }
}

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

internal static class RunRequestTimeRules
{
    internal static bool IsValidDateTimeOrMissing(string? value) =>
        value is null || IsValidRequiredDateTime(value);

    internal static bool IsValidDateTimeOrEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || IsValidRequiredDateTime(value);

    private static bool IsValidRequiredDateTime(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _);

    internal static bool SignupCloseTimeIsBeforeStartTime(string? signupCloseTime, string? startTime)
    {
        if (string.IsNullOrWhiteSpace(signupCloseTime))
            return true;

        if (!DateTimeOffset.TryParse(
                signupCloseTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var closeTime))
            return true;

        if (!DateTimeOffset.TryParse(
                startTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var runStart))
            return true;

        return closeTime < runStart;
    }
}
