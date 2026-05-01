// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using Xunit;

namespace Lfm.Api.Tests;

public sealed class LocalConfigurationContractTests
{
    public static TheoryData<string> RuntimeOptionKeyData => ToTheoryData(RuntimeOptionKeys);

    public static TheoryData<string> LegacyFlatKeyData => ToTheoryData(LegacyFlatKeys);

    public static TheoryData<string> DockerIgnoreRequiredPatternData => ToTheoryData(DockerIgnoreRequiredPatterns);

    public static TheoryData<string, string, string, string> ComposeServiceHardeningData =>
        new()
        {
            { "cosmosdb", "3g", "2.0", "512" },
            { "azurite", "512m", "0.5", "128" },
            { "functions", "1g", "1.0", "256" },
        };

    private static readonly string[] RuntimeOptionKeys =
    [
        "Blizzard__ClientId",
        "Blizzard__ClientSecret",
        "Blizzard__Region",
        "Blizzard__RedirectUri",
        "Blizzard__AppBaseUrl",
        "AZURE_FUNCTIONS_ENVIRONMENT",
        "Cosmos__Endpoint",
        "Cosmos__AuthKey",
        "Cosmos__DatabaseName",
        "Cosmos__ConnectionMode",
        "Cors__AllowedOrigins__0",
        "Storage__BlobConnectionString",
        "Auth__CookieName",
        "Auth__CookieMaxAgeHours",
        "Auth__KeyVaultUrl",
        "PrivacyContact__Email",
        "Audit__HashSalt",
    ];

    private static readonly string[] LegacyFlatKeys =
    [
        "LFM_CLIENT_ID",
        "LFM_CLIENT_SECRET",
        "BATTLE_NET_REGION",
        "BATTLE_NET_REDIRECT_URI",
        "APP_BASE_URL",
        "BATTLE_NET_COOKIE_SECURE",
        "COOKIE_DOMAIN",
        "COSMOS_ENDPOINT",
        "COSMOS_KEY",
        "COSMOS_DATABASE",
        "BLOB_STORAGE_URL",
        "PUBLIC_BLOB_STORAGE_URL",
        "HMAC_SECRET",
        "SESSION_ENCRYPTION_KEY",
        "KEY_VAULT_URL",
        "TEST_MODE",
    ];

    private static readonly string[] DockerIgnoreRequiredPatterns =
    [
        ".git/",
        ".env",
        ".env.*",
        "*.pem",
        "*.key",
        "*.p12",
        "*.pfx",
        "*.secret",
        "**/secrets.*",
        "id_rsa*",
        "api/local.settings.json",
        "app/wwwroot/appsettings.Development.json",
        ".azure/",
    ];

    [Theory]
    [MemberData(nameof(RuntimeOptionKeyData))]
    public void Example_env_documents_section_style_runtime_option(string key)
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.Contains(key, keys);
    }

    [Theory]
    [MemberData(nameof(LegacyFlatKeyData))]
    public void Example_env_omits_legacy_flat_runtime_option(string key)
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.DoesNotContain(key, keys);
    }

    [Theory]
    [MemberData(nameof(RuntimeOptionKeyData))]
    public void Docker_compose_functions_environment_uses_section_style_runtime_option(string key)
    {
        var keys = ReadComposeFunctionsEnvironmentKeys(
            Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));

        Assert.Contains(key, keys);
    }

    [Theory]
    [MemberData(nameof(LegacyFlatKeyData))]
    public void Docker_compose_functions_environment_omits_legacy_flat_runtime_option(string key)
    {
        var keys = ReadComposeFunctionsEnvironmentKeys(
            Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));

        Assert.DoesNotContain(key, keys);
    }

    [Theory]
    [MemberData(nameof(RuntimeOptionKeyData))]
    public void Readme_local_configuration_table_documents_section_style_runtime_option(string key)
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var localSection = ExtractBetween(readme, "### 2. Configure environment", "### 3. Start local stack");

        Assert.Contains($"`{key}`", localSection);
    }

    [Theory]
    [MemberData(nameof(LegacyFlatKeyData))]
    public void Readme_local_configuration_table_omits_legacy_flat_runtime_option(string key)
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var localSection = ExtractBetween(readme, "### 2. Configure environment", "### 3. Start local stack");

        Assert.DoesNotContain($"`{key}`", localSection);
    }

    [Theory]
    [MemberData(nameof(DockerIgnoreRequiredPatternData))]
    public void Dockerignore_excludes_secret_material_from_compose_build_context(string pattern)
    {
        var path = Path.Combine(FindRepositoryRoot(), ".dockerignore");

        Assert.True(File.Exists(path), ".dockerignore should exist at the repository root.");

        var patterns = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(pattern, patterns);
    }

    [Theory]
    [MemberData(nameof(ComposeServiceHardeningData))]
    public void Docker_compose_service_declares_container_hardening_controls(
        string service,
        string memLimit,
        string cpus,
        string pidsLimit)
    {
        var compose = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));
        var block = ExtractYamlBlock(compose, $"  {service}:");

        Assert.Contains("    read_only: true", block);
        Assert.Contains("    security_opt:", block);
        Assert.Contains("      - no-new-privileges:true", block);
        Assert.Contains("    cap_drop:", block);
        Assert.Contains("      - ALL", block);
        Assert.Contains($"    mem_limit: {memLimit}", block);
        Assert.Contains($"    cpus: \"{cpus}\"", block);
        Assert.Contains($"    pids_limit: {pidsLimit}", block);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "lfm.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
    }

    private static HashSet<string> ReadEnvKeys(string path)
    {
        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2)[0])
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ReadComposeFunctionsEnvironmentKeys(string path)
    {
        var lines = File.ReadAllLines(path);
        var inFunctions = false;
        var inEnvironment = false;
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            if (line == "  functions:")
            {
                inFunctions = true;
                continue;
            }

            if (inFunctions && line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("    ", StringComparison.Ordinal))
            {
                inFunctions = false;
                inEnvironment = false;
            }

            if (!inFunctions)
            {
                continue;
            }

            if (line == "    environment:")
            {
                inEnvironment = true;
                continue;
            }

            if (inEnvironment && line.StartsWith("    ", StringComparison.Ordinal) && !line.StartsWith("      ", StringComparison.Ordinal))
            {
                break;
            }

            if (inEnvironment)
            {
                var match = Regex.Match(line, @"^\s{6}([^:\s]+):");
                if (match.Success)
                {
                    keys.Add(match.Groups[1].Value);
                }
            }
        }

        return keys;
    }

    private static string ExtractBetween(string value, string startMarker, string endMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{startMarker} should exist.");

        var end = value.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"{endMarker} should exist after {startMarker}.");

        return value[start..end];
    }

    private static string ExtractYamlBlock(string value, string startMarker)
    {
        var start = value.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"{startMarker} should exist.");

        var nextSibling = Regex.Match(value[(start + startMarker.Length)..], @"\n  [^ \n][^:\n]*:");
        if (!nextSibling.Success)
        {
            return value[start..];
        }

        return value[start..(start + startMarker.Length + nextSibling.Index)];
    }

    private static TheoryData<string> ToTheoryData(IEnumerable<string> values)
    {
        var data = new TheoryData<string>();

        foreach (var value in values)
        {
            data.Add(value);
        }

        return data;
    }
}
