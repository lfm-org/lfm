using System.Diagnostics;
using Microsoft.Playwright;
using Testcontainers.CosmosDb;
using Testcontainers.Azurite;
using Xunit;

namespace Lfm.E2E.Fixtures;

public class StackFixture : IAsyncLifetime
{
    public CosmosDbContainer Cosmos { get; } = new CosmosDbBuilder().Build();
    public AzuriteContainer Azurite { get; } = new AzuriteBuilder().Build();
    public IBrowser Browser { get; private set; } = null!;
    public string AppBaseUrl { get; private set; } = "http://localhost:5001";
    public string ApiBaseUrl { get; private set; } = "http://localhost:7071";

    private Process? _apiProcess;
    private Process? _appProcess;
    private IPlaywright _playwright = null!;

    public virtual async Task InitializeAsync()
    {
        await Cosmos.StartAsync();
        await Azurite.StartAsync();

        _apiProcess = StartDotnet("api/Lfm.Api.csproj", new Dictionary<string, string>
        {
            ["Cosmos__Endpoint"] = Cosmos.GetConnectionString(),
            ["Cosmos__DatabaseName"] = "lfm-e2e",
            ["Cosmos__ConnectionMode"] = "Gateway",
            ["AzureWebJobsStorage"] = Azurite.GetConnectionString()
        });
        _appProcess = StartDotnet("app/Lfm.App.csproj", new());

        await WaitForHttp(ApiBaseUrl + "/api/health");
        await WaitForHttp(AppBaseUrl);

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public virtual async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.CloseAsync();
        _playwright?.Dispose();
        _apiProcess?.Kill(true);
        _appProcess?.Kill(true);
        await Cosmos.StopAsync();
        await Azurite.StopAsync();
    }

    private static Process StartDotnet(string project, Dictionary<string, string> env)
    {
        var psi = new ProcessStartInfo("dotnet", $"run --project {project} --no-build")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var (k, v) in env) psi.Environment[k] = v;
        return Process.Start(psi)!;
    }

    private static async Task WaitForHttp(string url, int timeoutSec = 60)
    {
        using var http = new HttpClient();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSec);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try { if ((await http.GetAsync(url)).IsSuccessStatusCode) return; }
            catch { }
            await Task.Delay(1000);
        }
        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
