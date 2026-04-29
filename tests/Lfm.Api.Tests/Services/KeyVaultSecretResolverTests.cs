// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Azure;
using Azure.Security.KeyVault.Secrets;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class KeyVaultSecretResolverTests
{
    [Fact]
    public async Task GetSecretAsync_caches_one_client_per_vault_url()
    {
        var factoryCalls = new List<string>();
        var clientMock = new Mock<SecretClient>();
        // Azure.Security.KeyVault.Secrets 4.10.0 added a `SecretContentType?`
        // parameter, so the virtual GetSecretAsync overload Moq must match has
        // four arguments: name, version, outContentType, cancellationToken.
        clientMock
            .Setup(c => c.GetSecretAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<SecretContentType?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                SecretModelFactory.KeyVaultSecret(new SecretProperties("k"), "value"),
                Mock.Of<Response>()));

        var resolver = new Lfm.Api.Services.KeyVaultSecretResolver(url =>
        {
            factoryCalls.Add(url);
            return clientMock.Object;
        });

        var v1 = await resolver.GetSecretAsync("https://kv-a.vault.azure.net/", "k1", CancellationToken.None);
        var v2 = await resolver.GetSecretAsync("https://kv-a.vault.azure.net/", "k2", CancellationToken.None);
        var v3 = await resolver.GetSecretAsync("https://kv-b.vault.azure.net/", "k3", CancellationToken.None);

        // Cache: the factory fires once per distinct URL, in arrival order.
        Assert.Equal(2, factoryCalls.Count);
        Assert.Equal("https://kv-a.vault.azure.net/", factoryCalls[0]);
        Assert.Equal("https://kv-b.vault.azure.net/", factoryCalls[1]);

        // End-to-end: the secret round-trips through the cached client.
        Assert.Equal("value", v1);
        Assert.Equal("value", v2);
        Assert.Equal("value", v3);
    }
}
