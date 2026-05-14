// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.RegularExpressions;
using System.Text.Json;
using Xunit;

namespace Lfm.Api.Tests;

public sealed class LocalConfigurationContractTests
{
    public static TheoryData<string> ExampleEnvKeyData => ToTheoryData(ExampleEnvKeys);

    public static TheoryData<string> ComposeFunctionsEnvironmentKeyData => ToTheoryData(ComposeFunctionsEnvironmentKeys);

    public static TheoryData<string> LegacyFlatKeyData => ToTheoryData(LegacyFlatKeys);

    public static TheoryData<string> RemovedExampleEnvKeyData => ToTheoryData(RemovedExampleEnvKeys);

    public static TheoryData<string> DockerIgnoreRequiredPatternData => ToTheoryData(DockerIgnoreRequiredPatterns);

    public static TheoryData<string, string, string, string> ComposeServiceHardeningData =>
        new()
        {
            { "cosmosdb", "3g", "2.0", "512" },
            { "azurite", "512m", "0.5", "128" },
            { "local-init", "512m", "0.5", "128" },
            { "functions", "1g", "1.0", "256" },
        };

    private static readonly string[] ExampleEnvKeys =
    [
        "Blizzard__ClientId",
        "Blizzard__ClientSecret",
        "Blizzard__Region",
        "Blizzard__RedirectUri",
        "Blizzard__AppBaseUrl",
        "Cors__AllowedOrigins__0",
        "Cosmos__Endpoint",
        "Cosmos__AuthKey",
        "Cosmos__DatabaseName",
        "Cosmos__ConnectionMode",
        "Local__AzuriteConnectionString",
        "Local__SiteAdminBattleNetIds",
        "Auth__CookieName",
        "Auth__CookieMaxAgeHours",
        "PrivacyContact__Email",
        "Audit__HashSalt",
    ];

    private static readonly string[] ComposeFunctionsEnvironmentKeys =
    [
        "AZURE_FUNCTIONS_ENVIRONMENT",
        "AzureWebJobsStorage",
        "Blizzard__ClientId",
        "Blizzard__ClientSecret",
        "Blizzard__Region",
        "Blizzard__RedirectUri",
        "Blizzard__AppBaseUrl",
        "Cors__AllowedOrigins__0",
        "Cosmos__Endpoint",
        "Cosmos__AuthKey",
        "Cosmos__DatabaseName",
        "Cosmos__ConnectionMode",
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

    private static readonly string[] RemovedExampleEnvKeys =
    [
        "AZURE_FUNCTIONS_ENVIRONMENT",
        "AzureWebJobsStorage",
        "Storage__BlobConnectionString",
        "Cosmos__EmulatorKeyContent",
        "COSMOS_KEY_CONTENT",
        "Auth__KeyVaultUrl",
        "Auth__LocalDevAllAuthenticatedUsersAreSiteAdmins",
        "PRIVACY_EMAIL",
        "EXPIRES_SECURITY_TXT",
        "SECURITY_POLICY_URL",
        "API_HOSTNAME",
        "FRONTEND_HOSTNAME",
        "STORAGE_ACCOUNT_NAME",
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
    [MemberData(nameof(ExampleEnvKeyData))]
    public void Example_env_documents_local_configuration_key(string key)
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.Contains(key, keys);
    }

    [Fact]
    public void Example_env_omits_all_caps_keys()
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.DoesNotContain(keys, static key =>
            key.Any(char.IsLetter) &&
            key.All(static c => !char.IsLetter(c) || char.IsUpper(c)));
    }

    [Theory]
    [MemberData(nameof(RemovedExampleEnvKeyData))]
    public void Example_env_omits_deploy_build_and_old_compose_keys(string key)
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.DoesNotContain(key, keys);
    }

    [Theory]
    [MemberData(nameof(LegacyFlatKeyData))]
    public void Example_env_omits_legacy_flat_runtime_option(string key)
    {
        var keys = ReadEnvKeys(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "example.env"));

        Assert.DoesNotContain(key, keys);
    }

    [Theory]
    [MemberData(nameof(ComposeFunctionsEnvironmentKeyData))]
    public void Docker_compose_functions_environment_uses_expected_configuration_key(string key)
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
    [MemberData(nameof(ExampleEnvKeyData))]
    public void Readme_local_configuration_table_documents_local_configuration_key(string key)
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var localTable = ExtractLocalConfigurationTable(readme);

        Assert.Contains($"`{key}`", localTable);
    }

    [Theory]
    [MemberData(nameof(LegacyFlatKeyData))]
    public void Readme_local_configuration_table_omits_legacy_flat_runtime_option(string key)
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var localTable = ExtractLocalConfigurationTable(readme);

        Assert.DoesNotContain($"| `{key}` |", localTable);
    }

    [Theory]
    [MemberData(nameof(RemovedExampleEnvKeyData))]
    public void Readme_local_configuration_table_omits_deploy_build_and_old_compose_keys(string key)
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var localTable = ExtractLocalConfigurationTable(readme);

        Assert.DoesNotContain($"| `{key}` |", localTable);
    }

    [Fact]
    public void Readme_deployment_configuration_documents_privacy_email_source_to_runtime_sink()
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "README.md"));
        var deploymentSection = ExtractBetween(readme, "## Deployment", "### Required GitHub Actions Secrets");

        Assert.Contains("`PRIVACY_EMAIL`", deploymentSection);
        Assert.Contains("`PrivacyContact__Email`", deploymentSection);
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

    [Fact]
    public void Docker_compose_cosmosdb_declares_writable_emulator_runtime_paths()
    {
        var compose = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));
        var block = ExtractYamlBlock(compose, "  cosmosdb:");

        Assert.Contains("        printf '%s' \"$Cosmos__AuthKey\" > /tmp/cosmos.key", block);
        Assert.Contains("      Cosmos__AuthKey: ${Cosmos__AuthKey}", block);
        Assert.DoesNotContain("Cosmos__EmulatorKeyContent", block);
        Assert.DoesNotContain("COSMOS_KEY_CONTENT", block);
        Assert.Contains("        mkdir -p /tmp/cosmos-workspace", block);
        Assert.Contains("        export HOME=/tmp/cosmos-workspace", block);
        Assert.Contains("        export WORKSPACE_ROOT=/tmp/cosmos-workspace", block);
        Assert.Contains("    read_only: true", block);
        Assert.Contains("      - /tmp", block);
        Assert.Contains("      - /logs:rw,mode=1777", block);
        Assert.Contains("      - /socket:rw,mode=1777", block);
        Assert.Contains("    volumes:", block);
        Assert.Contains("      - cosmos-data:/data", block);
        Assert.Contains("  cosmos-data:", compose);
    }

    [Fact]
    public void Docker_compose_maps_local_azurite_source_to_required_storage_sinks()
    {
        var compose = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));
        var block = ExtractYamlBlock(compose, "  functions:");

        Assert.Contains("      AzureWebJobsStorage: ${Local__AzuriteConnectionString}", block);
        Assert.Contains("      Storage__BlobConnectionString: ${Local__AzuriteConnectionString}", block);
    }

    [Fact]
    public void Docker_compose_runs_local_init_before_functions()
    {
        var compose = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));
        var dockerfile = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docker", "local-init", "Dockerfile"));
        var initBlock = ExtractYamlBlock(compose, "  local-init:");
        var block = ExtractYamlBlock(compose, "  functions:");

        Assert.Contains("      dockerfile: docker/local-init/Dockerfile", initBlock);
        Assert.Contains("dotnet tool install --tool-path /opt/cosmosdbshell CosmosDBShell --version 1.0.213-preview", dockerfile);
        Assert.Contains("ENV PATH=\"/opt/cosmosdbshell:${PATH}\"", dockerfile);
        Assert.Contains("WORKDIR /tmp", dockerfile);
        Assert.Contains("      - ./scripts/local-dev-init.sh:/usr/local/bin/local-dev-init.sh:ro", initBlock);
        Assert.Contains("    command: [\"bash\", \"/usr/local/bin/local-dev-init.sh\"]", initBlock);
        Assert.Contains("      Cosmos__Endpoint: ${Cosmos__Endpoint}", initBlock);
        Assert.Contains("      Local__SiteAdminBattleNetIds: ${Local__SiteAdminBattleNetIds:-}", initBlock);
        Assert.Contains("      - local-secrets:/home/local-secrets", initBlock);
        Assert.Contains("      local-init:", block);
        Assert.Contains("        condition: service_completed_successfully", block);
        Assert.Contains("      - local-secrets:/home/local-secrets:ro", block);
        Assert.Contains("      Auth__KeyVaultUrl: file:///home/local-secrets", block);
        Assert.DoesNotContain("Auth__LocalDevAllAuthenticatedUsersAreSiteAdmins", compose);
    }

    [Fact]
    public void Local_init_dockerfile_declares_non_root_runtime_user()
    {
        var dockerfile = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docker", "local-init", "Dockerfile"));

        Assert.Contains("useradd --system --uid 10001 --gid 10001", dockerfile);
        Assert.Contains("chown -R 10001:10001 /home/local-secrets /tmp/local-init-home", dockerfile);
        Assert.Contains("USER 10001:10001", dockerfile);
        Assert.DoesNotContain("USER root", dockerfile);
        Assert.DoesNotContain("USER 0", dockerfile);
    }

    [Fact]
    public void Local_dev_init_script_runs_setup_tool_from_repo_root()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "scripts", "local-dev-init.sh"));
        var toolProject = Path.Combine(root, "tools", "Lfm.LocalDevSetup", "Lfm.LocalDevSetup.csproj");

        Assert.False(File.Exists(toolProject), "Local compose setup should not add another .NET project.");
        Assert.Contains("cd \"$HOME\"", script);
        Assert.Contains("cosmosdbshell --connect \"$connection_string\" --connect-mode gateway -c \"$command\"", script);
        Assert.Contains("already exists|conflict|409", script);
        Assert.Contains("mkdb \\\"$Cosmos__DatabaseName\\\"", script);
        Assert.Contains("mkcon raiders /battleNetId --database=\\\"$Cosmos__DatabaseName\\\"", script);
        Assert.Contains("mkcon guilds /id --database=\\\"$Cosmos__DatabaseName\\\"", script);
        Assert.Contains("mkcon runs /id --database=\\\"$Cosmos__DatabaseName\\\"", script);
        Assert.Contains("mkcon idempotency /battleNetId --database=\\\"$Cosmos__DatabaseName\\\"", script);
        Assert.Contains("site-admin-battle-net-ids", script);
    }

    [Fact]
    public void Api_auth_options_do_not_expose_all_local_users_admin_bypass()
    {
        var root = FindRepositoryRoot();
        var authOptions = File.ReadAllText(Path.Combine(root, "api", "Options", "AuthOptions.cs"));
        var siteAdminService = File.ReadAllText(Path.Combine(root, "api", "Services", "SiteAdminService.cs"));

        Assert.DoesNotContain("LocalDevAllAuthenticatedUsersAreSiteAdmins", authOptions);
        Assert.DoesNotContain("LocalDevAllAuthenticatedUsersAreSiteAdmins", siteAdminService);
    }

    [Fact]
    public void Docker_compose_functions_declares_writable_host_secrets_path()
    {
        var compose = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LocalConfig", "docker-compose.local.yml"));
        var block = ExtractYamlBlock(compose, "  functions:");

        Assert.Contains("    read_only: true", block);
        Assert.Contains("      - /tmp", block);
        Assert.Contains("      - /azure-functions-host/Secrets:rw,mode=1777", block);
    }

    [Fact]
    public void Blazor_appsettings_points_at_local_compose_functions_endpoint()
    {
        var path = Path.Combine(FindRepositoryRoot(), "app", "wwwroot", "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var apiBaseUrl = document.RootElement.GetProperty("ApiBaseUrl").GetString();

        Assert.Equal("http://localhost:7071", apiBaseUrl);
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

    private static string ExtractLocalConfigurationTable(string readme)
    {
        var localSection = ExtractBetween(readme, "### 2. Configure environment", "### 3. Start local stack");

        return ExtractBetween(localSection, "| Variable | Required | Notes |", "\n\n");
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
