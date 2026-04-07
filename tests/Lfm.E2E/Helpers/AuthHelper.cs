using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Creates authenticated browser contexts by calling the test-only /api/e2e/login
/// endpoint, which sets a valid session cookie without going through Battle.net OAuth.
/// Requires E2E_TEST_MODE=true on the API process (set by StackFixture).
/// </summary>
public static class AuthHelper
{
    public static async Task<IBrowserContext> CreateAuthenticatedContextAsync(
        IBrowser browser,
        string apiBaseUrl,
        string appBaseUrl,
        string redirect = "/runs",
        string? battleNetId = null)
    {
        // Perform login via the E2E backdoor endpoint in a throw-away context.
        var setupContext = await browser.NewContextAsync();
        var setupPage = await setupContext.NewPageAsync();

        var loginUrl = $"{apiBaseUrl}/api/e2e/login?redirect={Uri.EscapeDataString(redirect)}";
        if (battleNetId != null)
        {
            loginUrl += $"&battleNetId={Uri.EscapeDataString(battleNetId)}";
        }
        await setupPage.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        // The endpoint sets the auth cookie and redirects to the app.
        // Wait for the redirect to complete (URL should contain the redirect path).
        await setupPage.WaitForURLAsync(new System.Text.RegularExpressions.Regex(
            System.Text.RegularExpressions.Regex.Escape(redirect) + @"(?:\?.*)?$"),
            new() { Timeout = 15000 });

        // Capture the storage state (cookies) to share with a fresh context.
        var stateFile = Path.GetTempFileName();
        await setupPage.Context.StorageStateAsync(new() { Path = stateFile });
        await setupContext.CloseAsync();

        return await browser.NewContextAsync(new()
        {
            BaseURL = appBaseUrl,
            StorageStatePath = stateFile,
        });
    }
}
