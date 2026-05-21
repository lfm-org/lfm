// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
using Lfm.OpenApiGenerator;
using Xunit;

namespace Lfm.Api.Tests.Openapi;

/// <summary>
/// Compile-time validation of the checked-in <c>api/openapi.yaml</c> contract.
/// Runs as part of the normal test suite so the existing <c>verify</c> CI gate
/// catches malformed specs — no separate lint workflow required. The yaml is
/// copied into the test output via <c>&lt;Content Include="..\..\api\openapi.yaml" .../&gt;</c>
/// in the csproj so the test is insensitive to the test runner's working
/// directory.
/// </summary>
public class OpenApiContractTests
{
    private static string SpecPath => Path.Combine(AppContext.BaseDirectory, "Contracts", "openapi.yaml");

    private static OpenApiReaderSettings CreateSettings()
    {
        var settings = new OpenApiReaderSettings();
        settings.AddYamlReader();
        return settings;
    }

    [Fact]
    public async Task Spec_parses_with_zero_diagnostic_errors()
    {
        Assert.True(File.Exists(SpecPath), $"openapi.yaml should be copied to test output at {SpecPath}");

        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.NotNull(result);
        Assert.NotNull(result.Document);
        Assert.NotNull(result.Diagnostic);
        Assert.Empty(result.Diagnostic.Errors);
    }

    [Fact]
    public async Task Spec_is_openapi_3_1()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.Equal(OpenApiSpecVersion.OpenApi3_1, result.Diagnostic!.SpecificationVersion);
    }

    [Fact]
    public async Task Spec_declares_info_and_servers()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.NotNull(result.Document!.Info);
        Assert.False(string.IsNullOrEmpty(result.Document.Info.Title));
        Assert.False(string.IsNullOrEmpty(result.Document.Info.Version));
        Assert.NotNull(result.Document.Servers);
        Assert.NotEmpty(result.Document.Servers);
    }

    [Fact]
    public async Task Spec_is_generated_snapshot()
    {
        var text = await File.ReadAllTextAsync(SpecPath);

        Assert.Contains("x-generated-by: tools/Lfm.OpenApiGenerator", text);
    }

    [Fact]
    public async Task Spec_matches_generated_output()
    {
        var text = (await File.ReadAllTextAsync(SpecPath)).ReplaceLineEndings("\n");
        var generated = OpenApiSnapshotGenerator.Generate();

        Assert.Equal(generated, text);
    }

    [Fact]
    public async Task Generated_deployment_snapshot_uses_supplied_server_url()
    {
        const string serverUrl = "https://api.example.test";
        var generated = OpenApiSnapshotGenerator.Generate(new OpenApiSnapshotOptions(serverUrl));
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.openapi.yaml");
        await File.WriteAllTextAsync(path, generated);
        try
        {
            var result = await OpenApiDocument.LoadAsync(path, CreateSettings());

            var server = Assert.Single(result.Document!.Servers!);
            Assert.Equal(serverUrl, server.Url);
            Assert.Contains(serverUrl, generated);
            Assert.DoesNotContain("url: /", generated, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Spec_does_not_publish_deployment_specific_servers()
    {
        var text = await File.ReadAllTextAsync(SpecPath);
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.DoesNotContain("dinosauruskeksi", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localhost:7071", text, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.Document!.Servers);
        Assert.All(result.Document.Servers!, server =>
        {
            var url = server.Url ?? string.Empty;
            Assert.False(
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase),
                $"OpenAPI server URL '{url}' must not pin one HTTP deployment as canonical.");
        });
    }

    [Fact]
    public async Task Spec_documents_health_endpoints()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.NotNull(result.Document!.Paths);
        Assert.Contains("/api/v1/health", result.Document.Paths.Keys);
        Assert.Contains("/api/v1/health/ready", result.Document.Paths.Keys);
    }

    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/guild")]
    [InlineData("/api/v1/guild/admin")]
    [InlineData("/api/v1/runs")]
    [InlineData("/api/v1/runs/{id}")]
    [InlineData("/api/v1/runs/{id}/signup")]
    [InlineData("/api/v1/raider/character")]
    [InlineData("/api/v1/raider/characters/{id}")]
    [InlineData("/api/v1/raider/characters/{id}/enrich")]
    [InlineData("/api/v1/battlenet/login")]
    [InlineData("/api/v1/battlenet/callback")]
    [InlineData("/api/v1/battlenet/logout")]
    [InlineData("/api/v1/battlenet/characters")]
    [InlineData("/api/v1/battlenet/characters/refresh")]
    [InlineData("/api/v1/battlenet/character-portraits")]
    [InlineData("/api/v1/wow/reference/expansions")]
    [InlineData("/api/v1/wow/reference/instances")]
    [InlineData("/api/v1/wow/reference/specializations")]
    [InlineData("/api/v1/wow/media/cache")]
    [InlineData("/api/v1/wow/reference/refresh")]
    [InlineData("/api/v1/admin/runs/migrate-schema")]
    [InlineData("/api/v1/privacy-contact/email")]
    public async Task Spec_documents_public_endpoint(string path)
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.NotNull(result.Document!.Paths);
        Assert.True(
            result.Document.Paths.ContainsKey(path),
            $"openapi.yaml should document {path} — runtime handler exists but the contract omits it.");
    }

    [Fact]
    public async Task Spec_declares_session_cookie_security_scheme()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        var schemes = result.Document!.Components?.SecuritySchemes;
        Assert.NotNull(schemes);
        Assert.True(schemes!.ContainsKey("sessionCookie"));
    }

    [Fact]
    public async Task Spec_defines_ProblemDetails_schema()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        var schemas = result.Document!.Components?.Schemas;
        Assert.NotNull(schemas);
        Assert.True(schemas!.ContainsKey("ProblemDetails"),
            "openapi.yaml must define ProblemDetails — every error response references it.");
    }

    [Fact]
    public async Task Spec_defines_signup_metadata_contract_fields()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        var schemas = result.Document!.Components?.Schemas;
        Assert.NotNull(schemas);
        Assert.True(schemas!.ContainsKey("CharacterSpecializationDto"),
            "openapi.yaml must define CharacterSpecializationDto for signup option specialization choices.");

        var characterSchema = schemas["CharacterDto"];
        Assert.NotNull(characterSchema.Properties);
        Assert.True(characterSchema.Properties.ContainsKey("specializations"),
            "CharacterDto.specializations must be documented for signup option specialization choices.");

        var runCharacterSchema = schemas["RunCharacterDto"];
        Assert.NotNull(runCharacterSchema.Properties);
        Assert.True(runCharacterSchema.Properties.ContainsKey("characterId"),
            "RunCharacterDto.characterId must be documented for current-user signup editing.");
        Assert.True(runCharacterSchema.Properties.ContainsKey("specId"),
            "RunCharacterDto.specId must be documented for current-user signup editing.");
    }
}
