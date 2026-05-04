// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Playwright;

namespace Lfm.E2E.Helpers;

public static class AuthHelper
{
    public static async Task<IBrowserContext> AuthenticatedContextAsync(
        IBrowser browser,
        string apiBaseUrl,
        string appBaseUrl,
        string battleNetId = "test-bnet-id",
        string redirect = "/runs")
    {
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await AuthenticatePageAsync(page, apiBaseUrl, appBaseUrl, battleNetId, redirect);
        await page.CloseAsync();

        return context;
    }

    public static async Task AuthenticatePageAsync(
        IPage page,
        string apiBaseUrl,
        string appBaseUrl,
        string battleNetId = "test-bnet-id",
        string redirect = "/runs")
    {
        var loginUrl = $"{apiBaseUrl}/api/e2e/login"
            + $"?battleNetId={Uri.EscapeDataString(battleNetId)}"
            + $"&redirect={Uri.EscapeDataString(redirect)}";
        await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForURLAsync($"{appBaseUrl}/**", new() { Timeout = 15000 });
    }

    public static async Task<IBrowserContext> OAuthAuthenticatedContextAsync(
        IBrowser browser,
        string appBaseUrl,
        string redirect = "/runs")
    {
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await AuthenticateThroughOAuthAsync(page, appBaseUrl, redirect);
        await page.CloseAsync();

        return context;
    }

    public static async Task AuthenticateThroughOAuthAsync(
        IPage page,
        string appBaseUrl,
        string redirect = "/runs")
    {
        var loginUrl = $"{appBaseUrl}/login?redirect={Uri.EscapeDataString(redirect)}";
        await page.GotoAsync(loginUrl, new() { WaitUntil = WaitUntilState.NetworkIdle });

        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in with Battle.net" })
            .ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Continue" })
            .ClickAsync(new() { Timeout = 15000 });

        await Assertions.Expect(page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex($"{System.Text.RegularExpressions.Regex.Escape(appBaseUrl)}{System.Text.RegularExpressions.Regex.Escape(redirect)}(\\?|$)"),
            new() { Timeout = 30000 });
    }

    public static Task<IBrowserContext> AnonymousContextAsync(IBrowser browser)
        => browser.NewContextAsync();
}
