using System.Diagnostics;
using System.Text;
using Microsoft.Playwright;
using Testcontainers.CosmosDb;
using Testcontainers.Azurite;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Fixtures;

public class StackFixture : IAsyncLifetime
{
    public CosmosDbContainer Cosmos { get; } = new CosmosDbBuilder().Build();
    public AzuriteContainer Azurite { get; } = new AzuriteBuilder().Build();
    public IBrowser Browser { get; private set; } = null!;
    public string AppBaseUrl => $"http://localhost:{_appPort}";
    public string ApiBaseUrl => $"http://localhost:{_apiPort}";

    private const int DefaultApiPort = 7171;
    private const int DefaultAppPort = 5199;
    private int _apiPort = DefaultApiPort;
    private int _appPort = DefaultAppPort;

    private Process? _apiProcess;
    private Process? _appProcess;
    private IPlaywright _playwright = null!;
    private readonly StringBuilder _apiOutput = new();
    private readonly StringBuilder _appOutput = new();

    /// <summary>
    /// Root of the git repository. StackFixture resolves project paths relative to this.
    /// Defaults to walking up from the test assembly location until lfm.sln is found.
    /// </summary>
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

    public virtual async Task InitializeAsync()
    {
        var repoRoot = FindRepoRoot();

        await Task.WhenAll(Cosmos.StartAsync(), Azurite.StartAsync());

        // Use unique ports to avoid conflicts with other running instances
        _apiPort = DefaultApiPort + Random.Shared.Next(0, 100);
        _appPort = DefaultAppPort + Random.Shared.Next(0, 100);

        _apiProcess = StartFunc(
            Path.Combine(repoRoot, "api"),
            _apiPort,
            new Dictionary<string, string>
            {
                ["Cosmos__Endpoint"] = Cosmos.GetConnectionString(),
                ["Cosmos__DatabaseName"] = "lfm-e2e",
                // IMPORTANT: the Linux Cosmos DB emulator only supports Gateway mode.
                ["Cosmos__ConnectionMode"] = "Gateway",
                ["AzureWebJobsStorage"] = Azurite.GetConnectionString(),
                ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated",
            },
            _apiOutput);

        _appProcess = StartDotnetRun(
            Path.Combine(repoRoot, "app", "Lfm.App.csproj"),
            repoRoot,
            _appPort,
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
            // Dump captured output to help diagnose startup failures
            throw new TimeoutException(
                $"Timed out waiting for stack.\n" +
                $"--- API output (port {_apiPort}) ---\n{_apiOutput}\n" +
                $"--- App output (port {_appPort}) ---\n{_appOutput}");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public virtual async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        KillProcess(_apiProcess);
        KillProcess(_appProcess);
        await Task.WhenAll(Cosmos.StopAsync(), Azurite.StopAsync());
    }

    /// <summary>
    /// Starts the Azure Functions host via `func start` (Azure Functions Core Tools).
    /// `dotnet run` on a Functions project does NOT serve HTTP endpoints.
    /// </summary>
    private static Process StartFunc(
        string projectDir,
        int port,
        Dictionary<string, string> env,
        StringBuilder output)
    {
        var psi = new ProcessStartInfo("func", $"start --port {port} --dotnet-isolated-debug")
        {
            WorkingDirectory = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start func in {projectDir}");
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine($"ERR: {e.Data}"); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
    }

    /// <summary>
    /// Starts the Blazor WASM app via `dotnet run` with a specific URL binding.
    /// </summary>
    private static Process StartDotnetRun(
        string csproj,
        string workingDir,
        int port,
        Dictionary<string, string> env,
        StringBuilder output)
    {
        var psi = new ProcessStartInfo("dotnet", $"run --project {csproj} --no-build --urls http://localhost:{port}")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start dotnet run for {csproj}");
        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine($"ERR: {e.Data}"); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
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
                // Connection refused, timeout, etc. — keep retrying
            }
            await Task.Delay(2000);
        }
        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
