// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

// The legitimate sub-lane S security tests — every test here exercises a
// browser-side enforcement contract that an HTTP-only integration test cannot
// prove. This spec replaces the deleted SecuritySpec.cs whose 19 tests all
// asserted server response headers without ever touching a browser (`E-HC-S1`).
//
// The tests below pin contracts the real Static Web Apps deployment enforces
// in production, replicated locally by StackFixture's Kestrel host (which sets
// the same globalHeaders the production platform sets).
[Collection("BrowserSecurity")]
[Trait("Category", E2ELanes.Security)]
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
            "/api/v1/me",
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

    // E2E scope: proves the auth cookie is hidden from document.cookie in Chromium.
    // Cheaper lanes cannot prove this because HttpOnly enforcement is browser cookie-jar behavior.
    // Shared data: read-only.
    [Fact]
    public async Task AuthCookie_NotAccessibleViaDocumentCookie()
    {
        // Pin the HttpOnly contract from the *browser's* perspective. The API
        // sets the cookie with HttpOnly=true (asserted at unit level by
        // BattleNetCallbackFunctionTests), but a regression in the cookie
        // attributes would only matter if the browser actually honours them.
        // This test proves the browser does.
        await AuthHelper.AuthenticatePageAsync(
            Page!,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var jsCookieView = await Page.EvaluateAsync<string>("() => document.cookie");

        Assert.DoesNotContain("battlenet_token", jsCookieView);

        // Sanity check: the cookie *does* exist in the browser jar — Playwright
        // can see it via the protocol because it bypasses the DOM. If this
        // fails the test setup is broken, not the contract under test.
        var jarCookies = await Context!.CookiesAsync();
        Assert.Contains(jarCookies, c => c.Name == "battlenet_token");
    }

    // E2E scope: proves credentialed cross-origin browser fetches are blocked by CORS.
    // Cheaper lanes cannot prove this because only the browser enforces response visibility.
    // Shared data: none.
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
                    const res = await fetch('{{fixture.Stack.ApiBaseUrl}}/api/v1/me', { credentials: 'include' });
                    return 'unexpected-success-status-' + res.status;
                } catch (e) {
                    return 'blocked';
                }
            }
            """);

        Assert.Equal("blocked", fetchOutcome);
    }

    // E2E scope: proves a cross-origin iframe cannot render the app.
    // Cheaper lanes cannot prove this because frame blocking is enforced by the browser.
    // Shared data: none.
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

    // E2E scope: proves injected inline script does not execute under the served CSP.
    // Cheaper lanes cannot prove this because CSP execution blocking is browser enforcement.
    // Shared data: none.
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

    // E2E scope: proves a tampered session cookie sends the SPA back to login.
    // Cheaper lanes cannot prove this because browser cookie state and client redirect must compose.
    // Shared data: read-only.
    [Fact]
    public async Task TamperedSessionCookie_AccessingProtectedRoute_RedirectsToLogin()
    {
        // Establish a real authenticated session, then corrupt the cookie so
        // the server cannot decrypt it. The server rejects the tampered cookie
        // with 401; the SPA must honour that rejection by routing the user to
        // /login. This proves browser-side handling of a rejected session —
        // the integration-layer CorsMiddlewareTests / AuthMiddlewareTests
        // prove the server-side rejection, but not the SPA's response to it.
        await AuthHelper.AuthenticatePageAsync(
            Page!,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var original = (await Context!.CookiesAsync())
            .First(c => c.Name == "battlenet_token");
        await Context.AddCookiesAsync(
        [
            new Cookie
            {
                Name = original.Name,
                Value = "TAMPERED-" + original.Value,
                Domain = original.Domain,
                Path = original.Path,
                HttpOnly = original.HttpOnly,
                Secure = original.Secure,
                SameSite = original.SameSite,
                Expires = original.Expires,
            },
        ]);

        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs");

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns"),
            new() { Timeout = 15000 });
    }

    // E2E scope: proves an expired browser session cookie is not sent to protected routes.
    // Cheaper lanes cannot prove this because expiry is enforced by the browser cookie jar.
    // Shared data: read-only.
    [Fact]
    public async Task ExpiredSessionCookie_AccessingProtectedRoute_RedirectsToLogin()
    {
        // Re-add the session cookie with an Expires timestamp in the past so
        // the Chromium cookie jar treats it as expired and drops it before
        // sending the request. The backend then sees an anonymous request,
        // returns 401, and the SPA routes to /login. Proves *browser*
        // cookie-jar expiry enforcement — even though the encrypted
        // principal inside the cookie is still valid, the browser's own
        // Expires check must prevent the cookie from leaving the jar.
        await AuthHelper.AuthenticatePageAsync(
            Page!,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        var original = (await Context!.CookiesAsync())
            .First(c => c.Name == "battlenet_token");
        var pastExpiry = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        await Context.AddCookiesAsync(
        [
            new Cookie
            {
                Name = original.Name,
                Value = original.Value,
                Domain = original.Domain,
                Path = original.Path,
                HttpOnly = original.HttpOnly,
                Secure = original.Secure,
                SameSite = original.SameSite,
                Expires = pastExpiry,
            },
        ]);

        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs");

        await Assertions.Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(@"/login\?redirect=%2Fruns"),
            new() { Timeout = 15000 });
    }

    // E2E scope: proves a cross-user delete attempt returns 403 through browser fetch.
    // Cheaper lanes cannot prove this because the browser must attach auth cookies and expose the response.
    // Shared data: read-only.
    [Fact]
    public async Task CrossUser_DeleteAnotherUsersRun_BlockedBy403()
    {
        // Auth-matrix gap (E-HC-S3 cross-user cell): a valid session for user
        // B must not be able to mutate user A's resource. Sub-lane S verifies
        // (a) the cookie issued for SecondaryBattleNetId is correctly attached
        // to a cross-origin fetch from the SPA, (b) the API authenticates the
        // request (so the response is 403, not 401), and (c) the browser
        // receives the 403 intact (no CORS layer collapses it to a network
        // error). Server-side rejection is unit-tested by RunAccessPolicyTests
        // and integration-tested by RunsDeleteFunctionTests; this proves the
        // browser path top-to-bottom.
        //
        // Setup details (see DefaultSeed.cs):
        //   - runs/e2e-run-001 is owned by PrimaryBattleNetId ("test-bnet-id").
        //   - SecondaryBattleNetId ("test-bnet-id-2") is in the same Test
        //     Guild but at rank 1, whose rankPermissions.canDeleteGuildRuns
        //     is false. The 403 therefore fires from the guild-rank-denied
        //     path in RunsDeleteFunction (line 75) — a cross-user denial
        //     even though both users share a guild.
        await AuthHelper.AuthenticatePageAsync(
            Page!,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId: DefaultSeed.SecondaryBattleNetId);

        // Land on the SPA origin so the auth cookie is in scope for the
        // cross-origin fetch (and the CORS request originates from the
        // configured allowed origin, not data:null as in the negative
        // CrossOriginFetch_FromUnregisteredOrigin_BlockedByCors test).
        await Page!.GotoAsync($"{fixture.Stack.AppBaseUrl}/runs",
            new() { WaitUntil = WaitUntilState.NetworkIdle });

        var encodedRunId = Uri.EscapeDataString(DefaultSeed.TestRunId);
        var status = await Page.EvaluateAsync<int>(
            $$"""
            async () => {
                const res = await fetch('{{fixture.Stack.ApiBaseUrl}}/api/v1/runs/{{encodedRunId}}', {
                    method: 'DELETE',
                    credentials: 'include',
                });
                return res.status;
            }
            """);

        Assert.Equal(403, status);
    }
}
