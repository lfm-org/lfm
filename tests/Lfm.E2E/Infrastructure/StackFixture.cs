// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;
using Xunit;

namespace Lfm.E2E.Infrastructure;

public class StackFixture : IAsyncLifetime
{
    public const string DatabaseName = "lfm-e2e";

    // Testcontainers 4.x removed the parameterless builder constructors; the
    // image must be passed explicitly. Bind container port 8081 to host port
    // 8081 so the SDK's endpoint-rediscovery (which always returns
    // http://127.0.0.1:8081/ from the gateway's writableLocations) reaches
    // the same socket Testcontainers exposed.
    //
    // Pinned by digest (not just the floating `:vnext-preview` tag) so CI
    // pulls the same bytes every run and E2E behaviour doesn't shift when
    // Microsoft repushes the preview tag. See issue #46. Bump when a newer
    // vnext-preview is desired, or when a stable version tag finally ships
    // and we can drop digest pinning.
    private readonly CosmosDbContainer _cosmos = new CosmosDbBuilder(
        "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview"
        + "@sha256:ed28d92b38aff69ccb4dbf439c584449f06432619f3415f429c09e4097cbe577")
        .WithPortBinding(8081, 8081)
        .WithStartupCallback(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        })
        .Build();

    // Version tag + manifest-list digest, matching the Cosmos pin above. The
    // :3.28.0 tag documents which Azurite release the digest corresponds to;
    // the @sha256: digest (on the multi-arch manifest list) guarantees the
    // same bytes every run and still resolves to the correct platform
    // manifest (linux/amd64 on CI, linux/arm64 on Apple-silicon dev). Bump
    // both together when upgrading.
    private readonly AzuriteContainer _azurite = new AzuriteBuilder(
        "mcr.microsoft.com/azure-storage/azurite:3.28.0"
        + "@sha256:b2edf4c05060390f368fef3dde4b82981b7125c763a3c6fdeb16e74b20094375")
        .WithPortBinding(10000, 10000)
        .WithPortBinding(10001, 10001)
        .WithPortBinding(10002, 10002)
        .Build();

    public IBrowser Browser { get; private set; } = null!;
    public CosmosClient CosmosClient { get; private set; } = null!;
    public string ApiBaseUrl => $"http://localhost:{_apiPort}";
    public string AppBaseUrl => $"http://localhost:{_appPort}";
    // Azurite connection string used by E2E seeders and passed to the API as
    // Storage__BlobConnectionString so BlobReferenceClient binds to the
    // containerised Azurite instead of trying managed identity at startup.
    public string BlobConnectionString => _azurite.GetConnectionString();

    /// <summary>
    /// Recent API process stdout/stderr for test failure diagnostics. The
    /// buffer accumulates for the lifetime of the fixture; tests that need
    /// to dump the log on failure can read this property and pass it to
    /// their test output helper.
    /// </summary>
    public string ApiProcessLog => _apiOutput.ToString();

    private const int DefaultApiPort = 7171;
    private const int DefaultAppPort = 5199;
    private int _apiPort = DefaultApiPort;
    private int _appPort = DefaultAppPort;

    private Process? _apiProcess;
    private WebApplication? _appHost;
    private WireMockServer? _oauthStub;
    private IPlaywright? _playwright;
    private readonly StringBuilder _apiOutput = new();

    /// <summary>
    /// Base URL of the in-process WireMock server that stubs the Battle.net
    /// OAuth endpoints (<c>/oauth/authorize</c>, <c>/oauth/token</c>,
    /// <c>/oauth/userinfo</c>). The API's OAuth client is pointed at this URL
    /// via the <c>Blizzard__OAuthBaseUrl</c> env var so the production callback
    /// flow can be exercised end-to-end without touching real Battle.net.
    /// </summary>
    public string OAuthStubBaseUrl => _oauthStub?.Url
        ?? throw new InvalidOperationException("OAuth stub has not been started yet.");

    public async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

        _apiPort = DefaultApiPort + Random.Shared.Next(0, 100);
        _appPort = DefaultAppPort + Random.Shared.Next(0, 100);

        await Task.WhenAll(_cosmos.StartAsync(), _azurite.StartAsync());

        CosmosClient = new CosmosClient(
            _cosmos.GetConnectionString(),
            new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                RequestTimeout = TimeSpan.FromSeconds(180),
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                },
                HttpClientFactory = () =>
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                    };
                    return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
                },
            });

        // Start the OAuth stub server before publishing the API so we know its
        // URL when constructing the func-start environment variables.
        _oauthStub = StartOAuthStub();

        // Publish API
        var apiPublishDir = Path.Combine(Path.GetTempPath(), $"lfm-e2e-api-{_apiPort}");
        var publishExitCode = RunProcess("dotnet",
            $"publish {Path.Combine(repoRoot, "api", "Lfm.Api.csproj")} -c Release -p:E2ETest=true -o {apiPublishDir}",
            repoRoot, timeoutSec: 120);
        if (publishExitCode != 0)
            throw new InvalidOperationException($"dotnet publish failed with exit code {publishExitCode}");

        // Start Functions host. CORS is handled by CorsMiddleware in the worker pipeline.
        // Do NOT use --cors flag — it conflicts with middleware by adding its own handler
        // that omits Access-Control-Allow-Credentials.
        _apiProcess = StartBackground("func", $"start --port {_apiPort} --no-build",
            apiPublishDir,
            new Dictionary<string, string>
            {
                ["Cosmos__Endpoint"] = ExtractConnectionStringPart(_cosmos.GetConnectionString(), "AccountEndpoint"),
                ["Cosmos__AuthKey"] = ExtractConnectionStringPart(_cosmos.GetConnectionString(), "AccountKey"),
                ["Cosmos__DatabaseName"] = DatabaseName,
                ["Cosmos__ConnectionMode"] = "Gateway",
                ["Cosmos__SkipCertValidation"] = "true",
                ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
                // Storage__BlobConnectionString drives BlobReferenceClient in
                // api/Program.cs. Must point at the same Azurite the seeder
                // uploads reference fixtures into — otherwise /api/wow/reference/instances
                // and /api/wow/reference/specializations see an empty container.
                ["Storage__BlobConnectionString"] = _azurite.GetConnectionString(),
                ["Storage__WowContainerName"] = Seeds.WowReferenceSeed.ContainerName,
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
                ["E2E_TEST_MODE"] = "true",
                ["Auth__CookieName"] = "battlenet_token",
                ["Auth__CookieMaxAgeHours"] = "24",
                ["Blizzard__ClientId"] = "e2e-stub",
                ["Blizzard__ClientSecret"] = "e2e-stub",
                ["Blizzard__Region"] = "eu",
                ["Blizzard__RedirectUri"] = $"http://localhost:{_apiPort}/api/battlenet/callback",
                ["Blizzard__AppBaseUrl"] = $"http://localhost:{_appPort}",
                ["Blizzard__OAuthBaseUrl"] = _oauthStub.Url ?? throw new InvalidOperationException(
                    "WireMock OAuth stub did not return a base URL after start"),
                ["Cors__AllowedOrigins__0"] = $"http://localhost:{_appPort}",
                ["PRIVACY_EMAIL"] = "privacy@e2e.test",
            },
            _apiOutput);

        // Publish the Blazor WASM app so we serve precompiled wwwroot files —
        // the same shape Static Web Apps serves in production. Avoids the dev
        // server entirely and the orphan-grandchild process tree it produced.
        var appPublishDir = Path.Combine(Path.GetTempPath(), $"lfm-e2e-app-{_appPort}");
        var appPublishExitCode = RunProcess("dotnet",
            $"publish {Path.Combine(repoRoot, "app", "Lfm.App.csproj")} -c Release -o {appPublishDir}",
            repoRoot, timeoutSec: 240);
        if (appPublishExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet publish (app) failed with exit code {appPublishExitCode}");
        var appWwwroot = Path.Combine(appPublishDir, "wwwroot");

        // Inject the dynamic API port into the published wwwroot's appsettings.json.
        // Writing into the publish dir (not the git-tracked source) means a killed
        // test process leaves no residue behind in the working tree.
        await File.WriteAllTextAsync(Path.Combine(appWwwroot, "appsettings.json"),
            $$"""
            {
              "ApiBaseUrl": "http://localhost:{{_apiPort}}",
              "Logging": {
                "LogLevel": {
                  "Default": "Warning",
                  "Microsoft.AspNetCore": "Error"
                }
              }
            }
            """);

        // Normalize env-specific appsettings the publish step may have copied.
        // Blazor's WebAssemblyHostConfiguration fetches appsettings.{Environment}.json
        // at startup and fails with System.Text.Json ExpectedJsonTokens when the
        // body is empty — which happens when the source files are sandbox-masked to
        // /dev/null and publish copies them as zero-byte artifacts. Overwriting
        // each env variant with a valid empty-object JSON keeps the config loader
        // happy regardless of the host filesystem's state. Matches the build env
        // name set by WasmApplicationEnvironmentName=Production in Lfm.App.csproj.
        foreach (var envFile in new[]
        {
            "appsettings.Production.json",
            "appsettings.Development.json",
            "appsettings.Local.json",
        })
        {
            var envPath = Path.Combine(appWwwroot, envFile);
            if (File.Exists(envPath))
            {
                await File.WriteAllTextAsync(envPath, "{}");
            }
        }

        try
        {
            await WaitForHttp($"http://localhost:{_apiPort}/api/health", timeoutSec: 120);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Timed out waiting for API.\n" +
                $"--- API output (port {_apiPort}) ---\n{_apiOutput}");
        }

        // Build and start the in-process Kestrel host that serves the published
        // wwwroot. This is the local equivalent of Static Web Apps in production.
        _appHost = BuildAppHost(repoRoot, appPublishDir, appWwwroot, _appPort, _apiPort);
        await _appHost.StartAsync();

        _playwright = await Playwright.CreateAsync();

        // Allow pointing Playwright at a locally-provided Chromium binary for environments
        // where Playwright's auto-download cache is unavailable (sandboxed CI, offline runs).
        // Null → Playwright uses its bundled browser, preserving default behaviour.
        var chromiumExecutablePath = Environment.GetEnvironmentVariable("LFM_E2E_CHROMIUM_PATH");
        Browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            ExecutablePath = string.IsNullOrWhiteSpace(chromiumExecutablePath) ? null : chromiumExecutablePath,
        });
    }

    public async Task DisposeAsync()
    {
        // Order matters: stop the browser first (drops any pending requests),
        // then the in-process app host (so it stops accepting new requests
        // before its dependencies disappear), then the API process, then the
        // backing data stores. The composition lets us sequence cleanly with
        // none of the dev-server grandchild orphan races the old `dotnet run`
        // approach forced us to brute-force around.
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_appHost is not null)
        {
            try { await _appHost.StopAsync(); } catch { /* best effort */ }
            await _appHost.DisposeAsync();
        }
        KillProcess(_apiProcess);
        // The OAuth stub has no state that survives the process, so we can
        // stop it after the API without risk of in-flight requests hanging.
        _oauthStub?.Stop();
        _oauthStub?.Dispose();
        CosmosClient?.Dispose();
        await Task.WhenAll(_azurite.StopAsync(), _cosmos.StopAsync());
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "lfm.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            "Could not find lfm.sln walking up from " + AppContext.BaseDirectory);
    }

    private static string ExtractConnectionStringPart(string connectionString, string key)
    {
        var prefix = key + "=";
        foreach (var part in connectionString.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return trimmed[prefix.Length..].TrimEnd('/');
        }
        throw new InvalidOperationException(
            $"Could not extract {key} from connection string: {connectionString}");
    }

    private static Process StartBackground(
        string fileName, string args, string workingDir,
        Dictionary<string, string> env, StringBuilder output)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName} in {workingDir}");
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine($"ERR: {e.Data}"); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
    }

    private static int RunProcess(string fileName, string args, string workingDir, int timeoutSec = 60)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)!;
        if (!proc.WaitForExit(timeoutSec * 1000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException($"{fileName} did not exit within {timeoutSec}s");
        }
        return proc.ExitCode;
    }

    private static void KillProcess(Process? proc)
    {
        if (proc is null || proc.HasExited) return;
        try { proc.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Start an in-process WireMock server that stubs the three Battle.net
    /// OAuth endpoints the API hits during the login flow:
    ///   - <c>GET /oauth/authorize</c>   — returns a 302 back to the API
    ///     callback URL with <c>code=&lt;fake&gt;&amp;state=&lt;echoed&gt;</c>
    ///     so the API's state validation passes.
    ///   - <c>POST /oauth/token</c>      — returns a canned access token.
    ///   - <c>GET /oauth/userinfo</c>    — returns a canned Battle.net user.
    /// The API's OAuth client is pointed at this server via
    /// <c>Blizzard__OAuthBaseUrl</c>. Replaces the real
    /// <c>https://{region}.battle.net</c> host so E2E tests can exercise the
    /// production callback flow hermetically.
    /// </summary>
    private static WireMockServer StartOAuthStub()
    {
        var server = WireMockServer.Start(new WireMockServerSettings
        {
            UseSSL = false,
            // Bind to an ephemeral port; the test infrastructure reads Url
            // back out and hands it to the API via the env var.
            StartAdminInterface = false,
        });

        // GET /oauth/authorize — echo state back to the API callback.
        // The Location template uses Handlebars so request.query.state and
        // request.query.redirect_uri come from the inbound request. WireMock
        // URL-decodes query parameters before exposing them to templates, so
        // the rendered Location is a real absolute URL (not double-encoded).
        server
            .Given(Request.Create().WithPath("/oauth/authorize").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(302)
                .WithHeader("Location",
                    "{{request.query.redirect_uri}}?code=e2e-fake-code&state={{request.query.state}}")
                .WithTransformer());

        // POST /oauth/token — canned access token.
        server
            .Given(Request.Create().WithPath("/oauth/token").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"access_token":"e2e-fake-access-token","token_type":"Bearer","expires_in":3600}"""));

        // GET /oauth/userinfo — canned Battle.net identity. The id is chosen
        // so the raider upsert creates a fresh raider in Cosmos for every run;
        // a numeric ID chosen to not collide with DefaultSeed's known ids.
        server
            .Given(Request.Create().WithPath("/oauth/userinfo").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"id":987654321,"battletag":"OAuthTest#1234"}"""));

        return server;
    }

    /// <summary>
    /// Build the in-process Kestrel host that serves the published Blazor
    /// wwwroot. Replicates the production Static Web Apps configuration: same
    /// static files, same global headers from <c>staticwebapp.config.json</c>,
    /// SPA fallback to <c>index.html</c> for client-routed paths.
    /// </summary>
    private static WebApplication BuildAppHost(
        string repoRoot, string contentRoot, string wwwroot, int appPort, int apiPort)
    {
        // Read the production global headers once at startup so any drift in
        // staticwebapp.config.json is visible at test time as well as at
        // unit-rubric contract-test time (StaticWebAppConfigContractTests).
        var swaConfigPath = Path.Combine(repoRoot, "app", "wwwroot", "staticwebapp.config.json");
        var swaConfig = JsonNode.Parse(File.ReadAllText(swaConfigPath))
            ?? throw new InvalidOperationException(
                $"staticwebapp.config.json at {swaConfigPath} is not valid JSON");
        var swaHeaders = swaConfig["globalHeaders"]?.AsObject()
            .ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<string>())
            ?? new Dictionary<string, string>();

        // The production CSP whitelists the production API origin in connect-src
        // and img-src. The local stack runs the API on a dynamically-assigned
        // localhost port, which would otherwise be blocked by the browser. Append
        // the local API origin everywhere the production host appears so the
        // local stack enforces a real CSP without breaking same-app requests.
        // The unit-rubric contract test (StaticWebAppConfigContractTests) still
        // pins the production CSP from the source file — this patch only affects
        // the in-memory header served by the local Kestrel host.
        if (swaHeaders.TryGetValue("Content-Security-Policy", out var csp))
        {
            // API_HOSTNAME matches the env var used by MSBuild template substitution;
            // the default mirrors the MSBuild default so the E2E stack works without env vars.
            var apiOrigin = $"https://{Environment.GetEnvironmentVariable("API_HOSTNAME") ?? "api.localhost"}";
            swaHeaders["Content-Security-Policy"] = csp.Replace(
                apiOrigin,
                $"{apiOrigin} http://localhost:{apiPort}");
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = wwwroot,
            Args = [],
        });
        builder.WebHost.UseUrls($"http://localhost:{appPort}");
        // Suppress ASP.NET Core's startup banner so it doesn't clutter test output.
        builder.Logging.ClearProviders();

        var fileProvider = new PhysicalFileProvider(wwwroot);
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        // .wasm is in the default provider as of net5+, but be explicit so a
        // future framework regression doesn't break Blazor streaming compilation.
        contentTypeProvider.Mappings[".wasm"] = "application/wasm";

        var app = builder.Build();

        // Global-header middleware runs first so even SPA fallback responses
        // carry the production headers.
        app.Use(async (ctx, next) =>
        {
            foreach (var (k, v) in swaHeaders)
                ctx.Response.Headers[k] = v;
            await next();
        });

        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ContentTypeProvider = contentTypeProvider,
            // Blazor publish output includes .blat, .dat, .dll, .pdb files that
            // the framework default provider does not know about. Serve them
            // generically — the browser treats them as opaque blobs.
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream",
        });

        // SPA fallback: every non-asset path returns index.html so the WASM
        // router can take over. Mirrors the navigationFallback rule in
        // staticwebapp.config.json.
        app.MapFallbackToFile("index.html");

        return app;
    }

    private static async Task WaitForHttp(string url, int timeoutSec = 60)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSec);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var resp = await http.GetAsync(url);
                if (resp.IsSuccessStatusCode) return;
            }
            catch
            {
                // Connection refused, timeout, etc.
            }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
