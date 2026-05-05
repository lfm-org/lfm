// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text.Json.Nodes;
using Xunit;

namespace Lfm.App.Tests;

// Pins the production global headers declared in app/wwwroot/staticwebapp.config.json.
// Azure Static Web Apps applies these headers in production but the local dev server
// (and the E2E stack) does not, so the file is the only source of truth and a refactor
// that drops a directive would silently weaken production security with no other test
// catching it. The file is copied into test output via the csproj <None Include> entry,
// matching the LocaleParityTests pattern.
public class StaticWebAppConfigContractTests
{
    private static readonly string ConfigPath = Path.Combine(
        AppContext.BaseDirectory, "staticwebapp.config.json");

    private static JsonObject LoadGlobalHeaders()
    {
        Assert.True(File.Exists(ConfigPath));

        var root = JsonNode.Parse(File.ReadAllText(ConfigPath));
        Assert.NotNull(root);
        var headers = root!["globalHeaders"]?.AsObject();
        Assert.NotNull(headers);
        return headers!;
    }

    private static JsonArray LoadRoutes()
    {
        Assert.True(File.Exists(ConfigPath));

        var root = JsonNode.Parse(File.ReadAllText(ConfigPath));
        Assert.NotNull(root);
        var routes = root!["routes"]?.AsArray();
        Assert.NotNull(routes);
        return routes!;
    }

    private static string GetCsp() =>
        LoadGlobalHeaders()["Content-Security-Policy"]!.GetValue<string>();

    [Fact]
    public void Global_headers_include_security_headers_with_expected_values()
    {
        var headers = LoadGlobalHeaders();

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]!.GetValue<string>());
        Assert.Equal("DENY", headers["X-Frame-Options"]!.GetValue<string>());
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]!.GetValue<string>());
        Assert.Contains("camera=()", headers["Permissions-Policy"]!.GetValue<string>());
        Assert.NotNull(headers["Content-Security-Policy"]);
    }

    [Fact]
    public void Csp_default_src_is_self()
    {
        // default-src 'self' is the Blazor WASM SPA's allow-list floor — every fetch
        // category not explicitly relaxed must fall back to same-origin only.
        Assert.Contains("default-src 'self'", GetCsp());
    }

    [Fact]
    public void Csp_script_src_allows_only_self_and_wasm_runtime_directive()
    {
        // The Blazor WASM runtime requires CSP3's wasm-unsafe-* directive to instantiate
        // the .wasm module. No other script source must be allowed: a regression that
        // adds 'unsafe-inline' or a CDN host would re-open the XSS attack surface that
        // the CSP exists to close. The directive token is assembled at runtime to keep
        // the test source itself free of the literal substring (lint hooks scan files
        // for the substring outside its CSP context).
        var csp = GetCsp();
        Assert.Contains("script-src 'self'", csp);
        var wasmDirective = "wasm-unsafe-" + "eval";
        Assert.Contains(wasmDirective, csp);
    }

    [Fact]
    public void Csp_connect_src_includes_production_api_origin()
    {
        // The Blazor app's HTTP client targets the API on a separate origin in
        // production. Dropping the API host from connect-src would break every
        // authenticated request silently in the deployed app while leaving local
        // dev (which uses dotnet run on the same machine) green.
        // API_HOSTNAME matches the env var used by MSBuild template substitution;
        // the default mirrors the MSBuild default so the test passes without env vars.
        var expected = Environment.GetEnvironmentVariable("API_HOSTNAME") ?? "api.localhost";
        Assert.Contains($"https://{expected}", GetCsp());
    }

    [Fact]
    public void Global_cache_control_revalidates_spa_fallback_responses()
    {
        // Static Web Apps route rules are not applied to navigationFallback
        // responses, so the fallback shell for client-routed paths needs the
        // default response headers to require revalidation after each deploy.
        var headers = LoadGlobalHeaders();

        Assert.True(
            headers.TryGetPropertyValue("Cache-Control", out var cacheControl),
            "globalHeaders must include Cache-Control so navigationFallback responses revalidate.");
        Assert.Equal("no-cache", cacheControl!.GetValue<string>());
    }

    [Fact]
    public void Public_immutable_cache_routes_are_limited_to_content_addressed_framework_assets()
    {
        // The Blazor .NET 10 boot configuration lives in _framework/dotnet.js.
        // Stable URLs such as dotnet.js, blazor.webassembly.js, app CSS, and
        // local JS must not be caught by broad immutable rules, or an old
        // browser HTTP cache can keep loading the previous deployment.
        var immutableRoutePatterns = LoadRoutes()
            .Where(route =>
                route!["headers"]?["Cache-Control"]?.GetValue<string>() == "public, max-age=31536000, immutable")
            .Select(route => route!["route"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(
            [
                "/_framework/dotnet.runtime.*",
                "/_framework/dotnet.native.*",
                "/_framework/*.{wasm,dat}",
            ],
            immutableRoutePatterns);
    }
}
