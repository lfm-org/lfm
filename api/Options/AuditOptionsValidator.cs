// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Options;

public sealed class AuditOptionsValidator(
    IHostEnvironment environment,
    IConfiguration configuration) : IValidateOptions<AuditOptions>
{
    private const string Failure =
        "Audit:HashSalt must be configured with a resolved secret value outside local development and E2E/test mode.";

    public ValidateOptionsResult Validate(string? name, AuditOptions options)
    {
        if (HasUsableHashSalt(options.HashSalt) || AllowsIdentityHasher())
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(Failure);
    }

    internal static bool HasUsableHashSalt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return !value.TrimStart().StartsWith("@Microsoft.KeyVault(", StringComparison.OrdinalIgnoreCase);
    }

    private bool AllowsIdentityHasher()
    {
        return environment.IsDevelopment()
            || string.Equals(environment.EnvironmentName, "Local", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(configuration["E2E_TEST_MODE"], "true", StringComparison.OrdinalIgnoreCase);
    }
}
