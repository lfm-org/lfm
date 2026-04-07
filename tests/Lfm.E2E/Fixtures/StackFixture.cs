using System.Diagnostics;
using System.Text;
using Microsoft.Azure.Cosmos;
using Microsoft.Playwright;
using Testcontainers.Azurite;
using Testcontainers.CosmosDb;
using Xunit;

namespace Lfm.E2E.Fixtures;

public class StackFixture : IAsyncLifetime
{
    public const string DatabaseName = "lfm-e2e";

    private readonly CosmosDbContainer _cosmos = new CosmosDbBuilder()
        .WithStartupCallback(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
        })
        .Build();

    private readonly AzuriteContainer _azurite = new AzuriteBuilder()
        .WithPortBinding(10000, 10000)
        .WithPortBinding(10001, 10001)
        .WithPortBinding(10002, 10002)
        .Build();

    public IBrowser Browser { get; private set; } = null!;
    public CosmosClient CosmosClient { get; private set; } = null!;
    public string ApiBaseUrl => $"http://localhost:{_apiPort}";
    public string AppBaseUrl => $"http://localhost:{_appPort}";

    private const int DefaultApiPort = 7171;
    private const int DefaultAppPort = 5199;
    private int _apiPort = DefaultApiPort;
    private int _appPort = DefaultAppPort;

    private Process? _apiProcess;
    private Process? _appProcess;
    private IPlaywright? _playwright;
    private readonly StringBuilder _apiOutput = new();
    private readonly StringBuilder _appOutput = new();

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

        // Publish API
        var apiPublishDir = Path.Combine(Path.GetTempPath(), $"lfm-e2e-api-{_apiPort}");
        var publishExitCode = RunProcess("dotnet",
            $"publish {Path.Combine(repoRoot, "api", "Lfm.Api.csproj")} -c Release -o {apiPublishDir}",
            repoRoot, timeoutSec: 120);
        if (publishExitCode != 0)
            throw new InvalidOperationException($"dotnet publish failed with exit code {publishExitCode}");

        // Start Functions host
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
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
                ["E2E_TEST_MODE"] = "true",
                ["Auth__CookieName"] = "battlenet_token",
                ["Auth__CookieMaxAgeHours"] = "24",
                ["Blizzard__ClientId"] = "e2e-stub",
                ["Blizzard__ClientSecret"] = "e2e-stub",
                ["Blizzard__Region"] = "eu",
                ["Blizzard__RedirectUri"] = $"http://localhost:{_apiPort}/api/battlenet/callback",
                ["Blizzard__AppBaseUrl"] = $"http://localhost:{_appPort}",
                ["Cors__AllowedOrigins__0"] = $"http://localhost:{_appPort}",
            },
            _apiOutput);

        // Start Blazor app
        _appProcess = StartBackground("dotnet",
            $"run --project {Path.Combine(repoRoot, "app", "Lfm.App.csproj")} -c Release --no-build --urls http://localhost:{_appPort}",
            repoRoot,
            new Dictionary<string, string>
            {
                ["ApiBaseUrl"] = $"http://localhost:{_apiPort}",
            },
            _appOutput);

        try
        {
            await WaitForHttp($"http://localhost:{_apiPort}/api/health", timeoutSec: 120);
            await WaitForHttp($"http://localhost:{_appPort}", timeoutSec: 60);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Timed out waiting for stack.\n" +
                $"--- API output (port {_apiPort}) ---\n{_apiOutput}\n" +
                $"--- App output (port {_appPort}) ---\n{_appOutput}");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        KillProcess(_appProcess);
        KillProcess(_apiProcess);
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
        proc.WaitForExit(timeoutSec * 1000);
        return proc.ExitCode;
    }

    private static void KillProcess(Process? proc)
    {
        if (proc is null || proc.HasExited) return;
        try { proc.Kill(entireProcessTree: true); }
        catch { /* best effort */ }
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
