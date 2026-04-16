// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    // Versionless Key Vault key URI (e.g. https://lfm-kv.vault.azure.net/keys/dataprotection).
    // Versionless is required to support automatic key rotation per MS Data Protection guidance.
    // When null/empty, Program.cs falls back to unencrypted filesystem persistence (local dev / E2E).
    public string? DataProtectionKeyUri { get; init; }

    public required string CookieName { get; init; } = "battlenet_token";
    public int CookieMaxAgeHours { get; init; } = 24;

    // Key Vault URL for reading the site-admin secret (e.g. https://lfm-kv.vault.azure.net).
    // When null/empty, isSiteAdmin always returns false (no Key Vault configured).
    public string? KeyVaultUrl { get; init; }
}
