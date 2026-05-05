// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

public class IndexHtmlTemplateContractTests
{
    private static readonly string TemplatePath = Path.Combine(
        AppContext.BaseDirectory, "index.html.template");
    private static readonly string GeneratedPath = Path.Combine(
        AppContext.BaseDirectory, "index.html");

    [Fact]
    public void Stable_deployment_entrypoint_assets_include_asset_version_query()
    {
        Assert.True(File.Exists(TemplatePath));

        var html = File.ReadAllText(TemplatePath);

        Assert.Contains("href=\"css/app.css?v={{ASSET_VERSION}}\"", html);
        Assert.Contains("href=\"Lfm.App.styles.css?v={{ASSET_VERSION}}\"", html);
        Assert.Contains("src=\"js/lfm-interop.js?v={{ASSET_VERSION}}\"", html);
        Assert.Contains("src=\"_framework/blazor.webassembly.js?v={{ASSET_VERSION}}\"", html);
    }

    [Fact]
    public void Generated_index_replaces_asset_version_placeholder()
    {
        Assert.True(File.Exists(GeneratedPath));

        var html = File.ReadAllText(GeneratedPath);

        Assert.DoesNotContain("{{ASSET_VERSION}}", html);
        Assert.Contains("src=\"_framework/blazor.webassembly.js?v=", html);
    }
}
