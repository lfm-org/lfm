// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using FluentValidation;

namespace Lfm.Contracts.Privacy;

/// <summary>
/// Request body for POST /api/privacy/contact.
/// Submits a privacy-related contact inquiry for logging to App Insights.
/// </summary>
public sealed record ContactRequest(
    string? Name,
    string? Email,
    string? Message,
    string? Type
);

/// <summary>
/// Validates ContactRequest.
/// </summary>
public sealed class ContactRequestValidator : AbstractValidator<ContactRequest>
{
    public ContactRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("name is required");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("email is required")
            .EmailAddress()
            .WithMessage("email must be a valid email address");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("message is required");

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("type is required");
    }
}
