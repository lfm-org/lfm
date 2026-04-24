// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.ComponentModel.DataAnnotations;

namespace Lfm.Api.Options;

/// <summary>
/// Privacy contact address surfaced by <c>GET /api/privacy-contact/email</c>.
/// Fork operators set the bound value via configuration (env:
/// <c>PrivacyContact__Email</c>); the value is validated at startup so a
/// typo produces a failed bind rather than a silent 404 at runtime.
/// </summary>
public sealed class PrivacyContactOptions
{
    public const string SectionName = "PrivacyContact";

    /// <summary>
    /// The privacy contact email for this deployment. Empty means "not
    /// configured", in which case the endpoint returns 404.
    /// </summary>
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
