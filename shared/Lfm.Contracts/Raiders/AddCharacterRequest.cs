// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using FluentValidation;

namespace Lfm.Contracts.Raiders;

public sealed record AddCharacterRequest(string? Region, string? Realm, string? Name);

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
