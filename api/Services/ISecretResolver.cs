// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

/// <summary>
/// Resolves a named secret from a URL-identified secret store. Introduced as a seam
/// around <c>Azure.Security.KeyVault.Secrets</c> so callers that read secrets can be
/// unit-tested without mocking the Azure SDK types directly.
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Returns the plain-text value of <paramref name="secretName"/> from the store
    /// at <paramref name="vaultUrl"/>, or <c>null</c> if the secret has no value.
    /// Implementations throw on transport, auth, or "not found" failures — callers
    /// decide whether to serve stale data or an empty set.
    /// </summary>
    Task<string?> GetSecretAsync(string vaultUrl, string secretName, CancellationToken ct);
}
