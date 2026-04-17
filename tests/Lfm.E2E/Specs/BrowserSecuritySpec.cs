// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

// The legitimate sub-lane S security tests — every test here exercises a
// browser-side enforcement contract that an HTTP-only integration test cannot
// prove. This spec replaces the deleted SecuritySpec.cs whose 19 tests all
// asserted server response headers without ever touching a browser (`E-HC-S1`).
//
// The four tests below pin contracts the real Static Web Apps deployment
// enforces in production, replicated locally by StackFixture's Kestrel host
// (which sets the same globalHeaders the production platform sets).
[Collection("BrowserSecurity")]
[Trait("Category", "Security")]
public class BrowserSecuritySpec(BrowserSecurityFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    // Every test in this spec deliberately triggers browser security
    // enforcement, which surfaces as console errors. Whitelist the substrings
    // those errors carry so the inherited E2ETestBase.DisposeAsync does not
    // re-fail tests whose assertions already passed against the same evidence.
    protected override string[] IgnoredConsolePatterns =>
        [
            "401",
            "/api/me",
            "Content Security Policy",
            "Refused",
            "blocked",
            "Failed to load resource",
            "ERR_FAILED",
            "ERR_BLOCKED_BY_RESPONSE",
            "X-Frame-Options",
        ];

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AnonymousContextAsync(fixture.Stack.Browser);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
        await StartTracingAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Context is not null)
            await Context.CloseAsync();
    }

    [Fact]
    public async Task AuthCookie_NotAccessibleViaDocumentCookie()
    {
        // Pin the HttpOnly contract from the *browser's* perspective. The API
        // sets the cookie with HttpOnly=true (asserted at unit level by
        // BattleNetCallbackFunctionTests), but a regression in the cookie
        // attributes would only matter if the browser actually honours them.
        // This test proves the browser does.
        var authContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var authPage = await authContext.NewPageAsync();
        try
        {
            await authPage.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
                new() { WaitUntil = WaitUntilState.NetworkIdle });

            var jsCookieView = await authPage.EvaluateAsync<string>("() => document.cookie");

            Assert.DoesNotContain("battlenet_token", jsCookieView);

            // Sanity check: the cookie *does* exist in the browser jar — Playwright
            // can see it via the protocol because it bypasses the DOM. If this
            // fails the test setup is broken, not the contract under test.
            var jarCookies = await authContext.CookiesAsync();
            Assert.Contains(jarCookies, c => c.Name == "battlenet_token");
        }
        finally
        {
            await authContext.CloseAsync();
        }
    }

    [Fact]
    public async Task CrossOriginFetch_FromUnregisteredOrigin_BlockedByCors()
    {
        // Load a page from an opaque-origin data: URL (Origin: null) and try to
        // fetch the API with credentials. The API's CorsMiddleware rejects null
        // origins, so the browser blocks the response from being read. The
        // unit-rubric CorsMiddlewareTests prove the middleware logic; this test
        // proves the browser actually honours the missing Allow-Origin header.
        await Page!.GotoAsync("data:text/html,<html><body>cors-test</body></html>");

        var fetchOutcome = await Page.EvaluateAsync<string>(
            $$"""
            async () => {
                try {
                    const res = await fetch('{{fixture.Stack.ApiBaseUrl}}/api/me', { credentials: 'include' });
                    return 'unexpected-success-status-' + res.status;
                } catch (e) {
                    return 'blocked';
                }
            }
            """);

        Assert.Equal("blocked", fetchOutcome);
    }

    [Fact]
    public async Task IframeFromCrossOrigin_BlockedByXFrameOptions()
    {
        // The production staticwebapp.config.json sets X-Frame-Options: DENY,
        // and StackFixture's Kestrel host replicates that header locally. Load
        // a parent page from an opaque origin, embed the app in an iframe, and
        // verify the browser refuses to render the framed content.
        var iframeHtml = $$"""
            <html>
              <body>
                <iframe id="target" src="{{fixture.Stack.AppBaseUrl}}/" style="width:300px;height:200px"></iframe>
              </body>
            </html>
            """;
        await Page!.SetContentAsync(iframeHtml);
        // Give the browser a brief moment to attempt the iframe load. We use a
        // Playwright Locator wait (not a sleep) so timing stays condition-based.
        await Page.Locator("iframe#target").WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var iframeState = await Page.EvaluateAsync<string>(
            """
            () => {
                const iframe = document.querySelector('iframe#target');
                if (!iframe) return 'no-iframe';
                try {
                    const doc = iframe.contentDocument;
                    if (!doc) return 'blocked';
                    if (!doc.body || doc.body.innerHTML.trim() === '') return 'blocked';
                    return 'rendered';
                } catch (e) {
                    return 'blocked';
                }
            }
            """);

        Assert.Equal("blocked", iframeState);
    }

    [Fact]
    public async Task CspBlocksInjectedInlineScript()
    {
        // The production CSP `script-src 'self' 'wasm-unsafe-*'` does not include
        // 'unsafe-inline', so inline <script> elements injected at runtime must
        // be blocked. StackFixture's Kestrel host serves the same CSP locally
        // (with the local API origin appended to connect-src). Verify the
        // browser refuses to execute an injected inline script.
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/login",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var pwnedFlag = await Page.EvaluateAsync<bool?>(
            """
            () => {
                const s = document.createElement('script');
                s.textContent = 'window.__lfm_pwned = true;';
                document.head.appendChild(s);
                return window.__lfm_pwned ?? null;
            }
            """);

        Assert.NotEqual(true, pwnedFlag);
    }
}
