// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.OpenApi;
using Microsoft.OpenApi.Reader;
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
    public async Task Spec_documents_health_endpoints()
    {
        var result = await OpenApiDocument.LoadAsync(SpecPath, CreateSettings());

        Assert.NotNull(result.Document!.Paths);
        Assert.Contains("/api/health", result.Document.Paths.Keys);
        Assert.Contains("/api/health/ready", result.Document.Paths.Keys);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/guild")]
    [InlineData("/api/guild/admin")]
    [InlineData("/api/runs")]
    [InlineData("/api/runs/{id}")]
    [InlineData("/api/runs/{id}/signup")]
    [InlineData("/api/raider/character")]
    [InlineData("/api/raider/characters/{id}")]
    [InlineData("/api/raider/characters/{id}/enrich")]
    [InlineData("/api/battlenet/login")]
    [InlineData("/api/battlenet/callback")]
    [InlineData("/api/battlenet/logout")]
    [InlineData("/api/battlenet/characters")]
    [InlineData("/api/battlenet/characters/refresh")]
    [InlineData("/api/battlenet/character-portraits")]
    [InlineData("/api/wow/reference/expansions")]
    [InlineData("/api/wow/reference/instances")]
    [InlineData("/api/wow/reference/specializations")]
    [InlineData("/api/wow/reference/refresh")]
    [InlineData("/api/admin/runs/migrate-schema")]
    [InlineData("/api/privacy-contact/email")]
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
}
