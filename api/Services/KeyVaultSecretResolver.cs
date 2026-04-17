// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Lfm.Api.Services;

/// <summary>
/// Production <see cref="ISecretResolver"/> backed by Azure Key Vault using
/// <see cref="DefaultAzureCredential"/>.
/// </summary>
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    public async Task<string?> GetSecretAsync(string vaultUrl, string secretName, CancellationToken ct)
    {
        var client = new SecretClient(new Uri(vaultUrl), new DefaultAzureCredential());
        var secret = await client.GetSecretAsync(secretName, cancellationToken: ct);
        return secret.Value.Value;
    }
}
