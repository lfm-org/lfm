// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Collections.Concurrent;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Lfm.Api.Services;

/// <summary>
/// Production <see cref="ISecretResolver"/> backed by Azure Key Vault using
/// <see cref="DefaultAzureCredential"/>. Caches one <see cref="SecretClient"/>
/// per vault URL — both the credential probe and the underlying HttpClient
/// are expensive to recreate.
/// </summary>
public sealed class KeyVaultSecretResolver : ISecretResolver
{
    private readonly Func<string, SecretClient> _factory;
    private readonly ConcurrentDictionary<string, SecretClient> _clients
        = new(StringComparer.OrdinalIgnoreCase);

    public KeyVaultSecretResolver()
        : this(url => new SecretClient(new Uri(url), new DefaultAzureCredential())) { }

    internal KeyVaultSecretResolver(Func<string, SecretClient> factory) => _factory = factory;

    public async Task<string?> GetSecretAsync(string vaultUrl, string secretName, CancellationToken ct)
    {
        var client = _clients.GetOrAdd(vaultUrl, _factory);
        var secret = await client.GetSecretAsync(secretName, cancellationToken: ct);
        return secret.Value.Value;
    }
}
