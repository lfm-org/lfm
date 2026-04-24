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
}
