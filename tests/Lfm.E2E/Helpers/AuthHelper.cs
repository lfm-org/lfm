using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

/// <summary>
/// Mirrors the TS auth fixture: navigates to the test-mode login endpoint once per context
/// and captures the resulting storage state (cookies/local storage) so the session is
/// shared across all pages created from that context.
/// </summary>
public static class AuthHelper
{
    /// <summary>
    /// Creates a new browser context that is already authenticated via the test-mode
    /// login endpoint. The caller owns disposal of the returned context.
    /// </summary>
    public static async Task<IBrowserContext> CreateAuthenticatedContextAsync(
        IBrowser browser,
        string apiBaseUrl,
        string appBaseUrl,
        string redirect = "/runs")
    {
        // Perform login in a throw-away page and capture the storage state as JSON.
        var setupContext = await browser.NewContextAsync();
        var setupPage = await setupContext.NewPageAsync();

        var loginUrl = $"{apiBaseUrl}/api/battlenet/login?redirect={Uri.EscapeDataString(redirect)}";
        await setupPage.GotoAsync(loginUrl);
        await Microsoft.Playwright.Assertions.Expect(setupPage)
            .ToHaveURLAsync(new System.Text.RegularExpressions.Regex(
                System.Text.RegularExpressions.Regex.Escape(redirect) + @"(?:\?.*)?$"));

        // StorageStateAsync returns JSON string — write to a temp file so NewContextAsync can read it.
        var stateFile = Path.GetTempFileName();
        await setupPage.Context.StorageStateAsync(new() { Path = stateFile });
        await setupContext.CloseAsync();

        // Create a fresh context seeded with the captured storage state.
        var context = await browser.NewContextAsync(new()
        {
            BaseURL = appBaseUrl,
            StorageStatePath = stateFile,
        });

        return context;
    }
}
