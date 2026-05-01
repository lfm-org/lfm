// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

/// <summary>
/// Audit-log privacy knobs. The only setting today is the salt used to
/// hash actor battleNetIds before they reach Application Insights. Produced
/// hashes are a stable HMAC-SHA-256 hex string bound to the configured
/// salt — correlatable across log entries within a deployment but not
/// reversible to the plaintext battleNetId.
/// </summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>
    /// HMAC key for hashing actor identifiers. Produce with
    /// <c>openssl rand -base64 32</c>, store in Key Vault, and wire to this
    /// setting via a <c>@Microsoft.KeyVault(...)</c> reference in Bicep.
    /// Leaving this empty disables hashing — actor ids are logged as
    /// plaintext. Startup validation permits that only in local development
    /// and E2E/test mode; any deployed environment that ingests logs to App
    /// Insights must set this to a resolved secret value.
    /// </summary>
    public string HashSalt { get; init; } = string.Empty;
}
