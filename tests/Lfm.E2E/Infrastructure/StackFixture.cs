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
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Xunit;

namespace Lfm.E2E.Infrastructure;

public class StackFixture : IAsyncLifetime
{
    public const string DatabaseName = "lfm-e2e";

    // Testcontainers 4.x removed the parameterless builder constructors; the
    // image must be passed explicitly. Keep a fixed host binding for the host
    // CosmosClient used by E2E seeders, and also attach the container to the
    // E2E network so the API container can address it by alias. The API sets
    // Cosmos__LimitToEndpoint=true so SDK endpoint discovery does not switch
    // from that alias back to the emulator's advertised 127.0.0.1 endpoint.
    //
    // Pinned by digest (not just the floating `:vnext-preview` tag) so CI
    // pulls the same bytes every run and E2E behaviour doesn't shift when
    // Microsoft repushes the preview tag. See issue #46. Bump when a newer
    // vnext-preview is desired, or when a stable version tag finally ships
    // and we can drop digest pinning.
    private readonly INetwork _network;
    private readonly CosmosDbContainer _cosmos;

    // Version tag + manifest-list digest, matching the Cosmos pin above. The
    // :3.35.0 tag documents which Azurite release the digest corresponds to;
    // the @sha256: digest (on the multi-arch manifest list) guarantees the
    // same bytes every run and still resolves to the correct platform
    // manifest (linux/amd64 on CI, linux/arm64 on Apple-silicon dev). Bump
    // both together when upgrading.
    private readonly AzuriteContainer _azurite;

    public IBrowser Browser { get; private set; } = null!;
    public CosmosClient CosmosClient { get; private set; } = null!;
    public string ApiBaseUrl => $"http://localhost:{_apiPort}";
    public string AppBaseUrl => $"http://localhost:{_appPort}";
    // Azurite connection string used by E2E seeders and passed to the API as
    // Storage__BlobConnectionString so BlobReferenceClient binds to the
    // containerised Azurite instead of trying managed identity at startup.
    public string BlobConnectionString => _azurite.GetConnectionString();

    /// <summary>
    /// Recent API container stdout/stderr for test failure diagnostics. The
    /// buffer accumulates for the lifetime of the fixture; tests that need
    /// to dump the log on failure can read this property and pass it to
    /// their test output helper.
    /// </summary>
    public string ApiProcessLog => _apiOutput.ToString();

    private const int DefaultApiPort = 7171;
    private const int DefaultAppPort = 5199;
    private const int ApiContainerPort = 80;
    private const string AzuriteNetworkAlias = "azurite";
    private const string CosmosNetworkAlias = "cosmos";
    private const string HostGatewayName = "host.docker.internal";
    private int _apiPort = DefaultApiPort;
    private int _appPort = DefaultAppPort;

    private IFutureDockerImage? _apiImage;
    private IContainer? _apiContainer;
    private WebApplication? _appHost;
    private MockOAuth2ProviderContainer? _oauthProvider;
    private IPlaywright? _playwright;
    private readonly StringBuilder _apiOutput = new();
    private string? _runtimeRoot;

    public StackFixture()
    {
        _network = new NetworkBuilder()
            .WithName($"lfm-e2e-{Guid.NewGuid():N}")
            .Build();

        _cosmos = new CosmosDbBuilder(
            "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview"
            + "@sha256:ed28d92b38aff69ccb4dbf439c584449f06432619f3415f429c09e4097cbe577")
            .WithNetwork(_network)
            .WithNetworkAliases(CosmosNetworkAlias)
            .WithPortBinding(8081, 8081)
            .WithStartupCallback(async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            })
            .Build();

        _azurite = new AzuriteBuilder(
            "mcr.microsoft.com/azure-storage/azurite:3.35.0"
            + "@sha256:647c63a91102a9d8e8000aab803436e1fc85fbb285e7ce830a82ee5d6661cf37")
            .WithNetwork(_network)
            .WithNetworkAliases(AzuriteNetworkAlias)
            .WithPortBinding(10000, 10000)
            .WithPortBinding(10001, 10001)
            .WithPortBinding(10002, 10002)
            .Build();
    }

    public string OAuthProviderAuthorizeEndpoint => _oauthProvider?.BrowserAuthorizeEndpoint
        ?? throw new InvalidOperationException("OAuth provider has not been started yet.");

    public async Task InitializeAsync()
    {
        try
        {
            await InitializeCoreAsync();
        }
        catch
        {
            try { await DisposeAsync(); } catch { /* preserve original startup failure */ }
            throw;
        }
    }

    private async Task InitializeCoreAsync()
    {
        var repoRoot = FindRepoRoot();
        _runtimeRoot = CreateRuntimeRoot(repoRoot);

        _apiPort = DefaultApiPort + Random.Shared.Next(0, 100);
        _appPort = DefaultAppPort + Random.Shared.Next(0, 100);

        await _network.CreateAsync();
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

        // Start the OAuth provider before launching the API so endpoint URLs
        // are known when constructing the container environment variables.
        _oauthProvider = new MockOAuth2ProviderContainer(repoRoot);
        await _oauthProvider.StartAsync();

        await StartApiContainerAsync(repoRoot);

        // Publish the Blazor WASM app so we serve precompiled wwwroot files —
        // the same shape Static Web Apps serves in production. Avoids the dev
        // server entirely and the orphan-grandchild process tree it produced.
        var appPublishDir = Path.Combine(_runtimeRoot, $"app-{_appPort}");
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
            await CaptureApiContainerLogsAsync();
            WriteApiLogArtifact();
            throw new TimeoutException(
                $"API container started, but the app did not answer /api/health on port {_apiPort}.\n" +
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
        // before its dependencies disappear), then the API container, then
        // the backing data stores. The composition lets us sequence cleanly
        // without host-local Functions Core Tools or dev-server grandchild
        // process races.
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        if (_appHost is not null)
        {
            try { await _appHost.StopAsync(); } catch { /* best effort */ }
            await _appHost.DisposeAsync();
        }
        await CaptureApiContainerLogsAsync();
        if (_apiContainer is not null)
        {
            try { await _apiContainer.StopAsync(); } catch { /* best effort */ }
            try { await _apiContainer.DisposeAsync(); } catch { /* best effort */ }
        }
        if (_apiImage is not null)
        {
            try { await _apiImage.DeleteAsync(); } catch { /* best effort */ }
        }
        if (_oauthProvider is not null)
        {
            await _oauthProvider.DisposeAsync();
        }
        WriteApiLogArtifact();
        CosmosClient?.Dispose();
        try { await _azurite.StopAsync(); } catch { /* best effort */ }
        try { await _cosmos.StopAsync(); } catch { /* best effort */ }
        try { await _network.DeleteAsync(); } catch { /* best effort */ }
        DeleteRuntimeRoot();
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

    private async Task StartApiContainerAsync(string repoRoot)
    {
        _apiImage = new ImageFromDockerfileBuilder()
            .WithName($"lfm-e2e-api:{Guid.NewGuid():N}")
            .WithDockerfileDirectory(repoRoot)
            .WithDockerfile(Path.Combine("api", "Dockerfile"))
            .WithBuildArgument("E2ETest", "true")
            .Build();

        try
        {
            await _apiImage.CreateAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to build the E2E API Docker image through Testcontainers. " +
                "Check that Docker is running and the pinned base images are available.",
                ex);
        }

        var apiEnvironment = CreateApiEnvironment();
        _apiContainer = new ContainerBuilder(_apiImage)
            .WithName($"lfm-e2e-api-{_apiPort}-{Guid.NewGuid():N}")
            .WithNetwork(_network)
            .WithNetworkAliases("api")
            .WithPortBinding(_apiPort, ApiContainerPort)
            .WithExtraHost(HostGatewayName, "host-gateway")
            .WithEnvironment(apiEnvironment)
            .WithCleanUp(true)
            .WithAutoRemove(true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(
                ApiContainerPort,
                wait => wait.WithTimeout(TimeSpan.FromSeconds(120))))
            .Build();

        try
        {
            await _apiContainer.StartAsync();
        }
        catch (Exception ex)
        {
            await CaptureApiContainerLogsAsync();
            WriteApiLogArtifact();
            throw new InvalidOperationException(
                "Failed to start the E2E API container through Testcontainers. " +
                "Check Docker container startup and the API container logs in artifacts/e2e-results/api.log.",
                ex);
        }
    }

    private Dictionary<string, string> CreateApiEnvironment()
    {
        var cosmosConnectionString = _cosmos.GetConnectionString();
        var apiStorageConnectionString = ToNetworkAliasConnectionString(
            _azurite.GetConnectionString(),
            AzuriteNetworkAlias);
        var oauthProvider = _oauthProvider
            ?? throw new InvalidOperationException("OAuth provider was not started before API configuration");

        // CORS is handled by CorsMiddleware in the worker pipeline. Do NOT use
        // the Functions host --cors flag; it conflicts with middleware by
        // adding its own handler that omits Access-Control-Allow-Credentials.
        return new Dictionary<string, string>
        {
            ["Cosmos__Endpoint"] = ToNetworkAliasUrl(
                ExtractConnectionStringPart(cosmosConnectionString, "AccountEndpoint"),
                CosmosNetworkAlias),
            ["Cosmos__AuthKey"] = ExtractConnectionStringPart(cosmosConnectionString, "AccountKey"),
            ["Cosmos__DatabaseName"] = DatabaseName,
            ["Cosmos__ConnectionMode"] = "Gateway",
            ["Cosmos__LimitToEndpoint"] = "true",
            ["Cosmos__SkipCertValidation"] = "true",
            ["AzureWebJobsStorage"] = apiStorageConnectionString,
            // Storage__BlobConnectionString drives BlobReferenceClient in
            // api/Program.cs. Must point at the same Azurite the seeder uploads
            // reference fixtures into; inside the API container, the same
            // Azurite container is reached through Docker network DNS.
            ["Storage__BlobConnectionString"] = apiStorageConnectionString,
            ["Storage__WowContainerName"] = Seeds.WowReferenceSeed.ContainerName,
            ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
            ["E2E_TEST_MODE"] = "true",
            ["Auth__CookieName"] = "battlenet_token",
            ["Auth__CookieMaxAgeHours"] = "24",
            ["Auth__KeyVaultUrl"] = "https://lfm-e2e-vault.vault.azure.net/",
            ["Blizzard__ClientId"] = "e2e-stub",
            ["Blizzard__ClientSecret"] = "e2e-stub",
            ["Blizzard__Region"] = "eu",
            ["Blizzard__RedirectUri"] = $"http://localhost:{_apiPort}/api/battlenet/callback",
            ["Blizzard__AppBaseUrl"] = $"http://localhost:{_appPort}",
            ["Blizzard__Scope"] = "openid wow.profile",
            ["Blizzard__AuthorizationEndpoint"] = oauthProvider.BrowserAuthorizeEndpoint,
            ["Blizzard__TokenEndpoint"] = oauthProvider.ApiTokenEndpoint,
            ["Blizzard__UserInfoEndpoint"] = oauthProvider.ApiUserInfoEndpoint,
            ["Cors__AllowedOrigins__0"] = $"http://localhost:{_appPort}",
            ["PRIVACY_EMAIL"] = "privacy@e2e.test",
            ["PrivacyContact__Email"] = "privacy@e2e.test",
            ["RateLimit__Enabled"] = "false",
        };
    }

    private static string ToNetworkAliasUrl(string url, string alias)
    {
        var builder = new UriBuilder(url)
        {
            Host = alias,
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    private static string ToNetworkAliasConnectionString(string connectionString, string alias)
    {
        return connectionString
            .Replace("127.0.0.1", alias, StringComparison.OrdinalIgnoreCase)
            .Replace("localhost", alias, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateRuntimeRoot(string repoRoot)
    {
        var runtimeRoot = Path.Combine(
            repoRoot,
            ".cache",
            "e2e-runtime",
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(runtimeRoot);
        return runtimeRoot;
    }

    private async Task CaptureApiContainerLogsAsync()
    {
        if (_apiContainer is null)
        {
            return;
        }

        try
        {
            var (stdout, stderr) = await _apiContainer.GetLogsAsync(default, default, timestampsEnabled: true);
            _apiOutput.Clear();
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _apiOutput.AppendLine(stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                _apiOutput.AppendLine("--- stderr ---");
                _apiOutput.AppendLine(stderr);
            }
        }
        catch (Exception ex)
        {
            _apiOutput.AppendLine($"Failed to read API container logs: {ex.Message}");
        }
    }

    private void DeleteRuntimeRoot()
    {
        if (string.IsNullOrWhiteSpace(_runtimeRoot) || !Directory.Exists(_runtimeRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(_runtimeRoot, recursive: true);
        }
        catch
        {
            // Best effort — a failed cleanup should not hide the test result.
        }
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

    private void WriteApiLogArtifact()
    {
        try
        {
            var dir = Path.Combine(FindRepoRoot(), "artifacts", "e2e-results");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "api.log"), _apiOutput.ToString());
        }
        catch
        {
            // Best effort — never hide the original test result.
        }
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
