// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Options;

public sealed class AuditOptionsValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@Microsoft.KeyVault(VaultName=lfm-kv;SecretName=audit-hash-salt)")]
    [InlineData("@Microsoft.KeyVault(SecretUri=https://lfm-kv.vault.azure.net/secrets/audit-hash-salt/)")]
    public void Validate_rejects_missing_or_unresolved_hash_salt_in_production(string hashSalt)
    {
        var validator = CreateValidator(Environments.Production);

        var result = validator.Validate(null, new AuditOptions { HashSalt = hashSalt });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Audit:HashSalt", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_accepts_configured_hash_salt_in_production()
    {
        var validator = CreateValidator(Environments.Production);

        var result = validator.Validate(null, new AuditOptions { HashSalt = "configured-salt" });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_allows_missing_hash_salt_in_development()
    {
        var validator = CreateValidator(Environments.Development);

        var result = validator.Validate(null, new AuditOptions { HashSalt = string.Empty });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_allows_missing_hash_salt_in_e2e_test_mode()
    {
        var validator = CreateValidator(Environments.Production, e2eTestMode: true);

        var result = validator.Validate(null, new AuditOptions { HashSalt = string.Empty });

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@Microsoft.KeyVault(VaultName=lfm-kv;SecretName=audit-hash-salt)")]
    public void HasUsableHashSalt_rejects_values_that_would_not_hash_actors(string hashSalt)
    {
        Assert.False(AuditOptionsValidator.HasUsableHashSalt(hashSalt));
    }

    [Fact]
    public void HasUsableHashSalt_accepts_resolved_secret_value()
    {
        Assert.True(AuditOptionsValidator.HasUsableHashSalt("resolved-secret-value"));
    }

    private static AuditOptionsValidator CreateValidator(string environmentName, bool e2eTestMode = false)
    {
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(x => x.EnvironmentName).Returns(environmentName);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["E2E_TEST_MODE"] = e2eTestMode ? "true" : "false",
            })
            .Build();

        return new AuditOptionsValidator(hostEnvironment.Object, config);
    }
}
