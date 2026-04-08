using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Xunit;

namespace Lfm.E2E.Fixtures;

[CollectionDefinition("Security")]
public class SecurityCollection : ICollectionFixture<SecurityFixture> { }

public class SecurityFixture : IAsyncLifetime
{
    public StackFixture Stack { get; private set; } = null!;

    /// <summary>
    /// Pre-configured HttpClient pointing at the API base URL.
    /// Uses a cookie container so tests can authenticate and make
    /// subsequent requests with the session cookie.
    /// </summary>
    public HttpClient ApiHttpClient { get; private set; } = null!;

    /// <summary>
    /// Pre-configured HttpClient pointing at the App (Blazor WASM) base URL.
    /// </summary>
    public HttpClient AppHttpClient { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Stack = await SharedStack.GetAsync();
        await DefaultSeed.SeedAsync(Stack.CosmosClient, StackFixture.DatabaseName);

        var apiHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
        };
        ApiHttpClient = new HttpClient(apiHandler)
        {
            BaseAddress = new Uri(Stack.ApiBaseUrl),
        };

        var appHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
        };
        AppHttpClient = new HttpClient(appHandler)
        {
            BaseAddress = new Uri(Stack.AppBaseUrl),
        };
    }

    public Task DisposeAsync()
    {
        ApiHttpClient?.Dispose();
        AppHttpClient?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a new HttpClient that has been authenticated as the given
    /// battle.net identity via the E2E login endpoint. The returned client
    /// carries the auth cookie for subsequent API calls.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string battleNetId = "test-bnet-id")
    {
        var cookieHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = true,
        };
        var client = new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri(Stack.ApiBaseUrl),
        };

        // Call the E2E login endpoint — it sets the auth cookie and returns
        // a redirect (302). We suppress auto-redirect so we can capture the
        // Set-Cookie header.
        var loginUrl = $"/api/e2e/login?battleNetId={Uri.EscapeDataString(battleNetId)}"
            + "&redirect=%2Fruns";
        var response = await client.GetAsync(loginUrl);
        // The cookie container automatically stores the Set-Cookie header.
        return client;
    }
}
