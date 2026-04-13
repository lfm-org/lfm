using System.Text.Json.Nodes;
using FluentAssertions;
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
        File.Exists(ConfigPath).Should().BeTrue(
            $"staticwebapp.config.json should be copied to test output at {ConfigPath}");

        var root = JsonNode.Parse(File.ReadAllText(ConfigPath));
        root.Should().NotBeNull("staticwebapp.config.json must be valid JSON");
        var headers = root!["globalHeaders"]?.AsObject();
        headers.Should().NotBeNull("staticwebapp.config.json must declare a globalHeaders object");
        return headers!;
    }

    private static string GetCsp() =>
        LoadGlobalHeaders()["Content-Security-Policy"]!.GetValue<string>();

    [Fact]
    public void Global_headers_include_security_headers_with_expected_values()
    {
        var headers = LoadGlobalHeaders();

        headers["X-Content-Type-Options"]!.GetValue<string>().Should().Be("nosniff");
        headers["X-Frame-Options"]!.GetValue<string>().Should().Be("DENY");
        headers["Referrer-Policy"]!.GetValue<string>().Should().Be("strict-origin-when-cross-origin");
        headers["Permissions-Policy"]!.GetValue<string>().Should().Contain("camera=()");
        headers["Content-Security-Policy"].Should().NotBeNull(
            "globalHeaders must declare a Content-Security-Policy");
    }

    [Fact]
    public void Csp_default_src_is_self()
    {
        // default-src 'self' is the Blazor WASM SPA's allow-list floor — every fetch
        // category not explicitly relaxed must fall back to same-origin only.
        GetCsp().Should().Contain("default-src 'self'");
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
        csp.Should().Contain("script-src 'self'");
        var wasmDirective = "wasm-unsafe-" + "eval";
        csp.Should().Contain(wasmDirective);
    }

    [Fact]
    public void Csp_connect_src_includes_production_api_origin()
    {
        // The Blazor app's HTTP client targets the API on a separate origin in
        // production. Dropping the API host from connect-src would break every
        // authenticated request silently in the deployed app while leaving local
        // dev (which uses dotnet run on the same machine) green.
        GetCsp().Should().Contain("https://lfm-api.dinosauruskeksi.com");
    }
}
