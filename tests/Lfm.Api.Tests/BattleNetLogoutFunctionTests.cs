using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Moq;
using Lfm.Api.Auth;
using Lfm.Api.Functions;
using Lfm.Api.Options;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

public class BattleNetLogoutFunctionTests
{
    private const string AppBaseUrl = "https://example.com";
    private const string FakeCookieName = "battlenet_token";

    private static FunctionContext MakeFunctionContext(SessionPrincipal principal)
    {
        var items = new Dictionary<object, object> { [SessionKeys.Principal] = principal };
        var ctx = new Mock<FunctionContext>();
        ctx.Setup(c => c.Items).Returns(items);
        return ctx.Object;
    }

    private static SessionPrincipal MakePrincipal(string battleNetId = "bnet-1") =>
        new SessionPrincipal(
            BattleNetId: battleNetId,
            BattleTag: "Player#1234",
            GuildId: "42",
            GuildName: "Test Guild",
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1));

    private static BattleNetLogoutFunction MakeFunction(
        TestLogger<BattleNetLogoutFunction>? logger = null)
    {
        var blizzardOpts = MsOptions.Create(new BlizzardOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret",
            Region = "eu",
            RedirectUri = "https://example.com/api/battlenet/callback",
            AppBaseUrl = AppBaseUrl,
        });
        var authOpts = MsOptions.Create(new AuthOptions
        {
            DataProtectionKeyUri = "https://kv.example.com/keys/dp",
            CookieName = FakeCookieName,
            CookieMaxAgeHours = 24,
        });
        return new BattleNetLogoutFunction(
            blizzardOpts,
            authOpts,
            logger ?? new TestLogger<BattleNetLogoutFunction>());
    }

    [Fact]
    public void Run_redirects_to_app_base_url()
    {
        var fn = MakeFunction();
        var ctx = MakeFunctionContext(MakePrincipal());
        var req = new DefaultHttpContext().Request;

        var result = fn.Run(req, ctx);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(AppBaseUrl, redirect.Url);
        Assert.False(redirect.Permanent);
    }

    [Fact]
    public void Run_clears_auth_cookie_via_expires_in_the_past()
    {
        // ASP.NET Core's IResponseCookies.Delete writes expires=Thu, 01 Jan 1970 (with
        // an empty value) — pin to the exact shape the SUT produces. The cookie must
        // have HttpOnly and the configured cookie name.
        var fn = MakeFunction();
        var ctx = MakeFunctionContext(MakePrincipal());
        var httpContext = new DefaultHttpContext();

        fn.Run(httpContext.Request, ctx);

        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].OfType<string>().ToArray();
        var authCookie = setCookieHeaders.SingleOrDefault(h => h.Contains(FakeCookieName));
        Assert.NotNull(authCookie);
        Assert.Contains("expires=thu, 01 jan 1970", authCookie!.ToLowerInvariant());
        Assert.Contains("path=/", authCookie.ToLowerInvariant());
        Assert.Contains("httponly", authCookie.ToLowerInvariant());
        Assert.StartsWith(FakeCookieName + "=", authCookie);
    }

    // -----------------------------------------------------------------------
    // Audit events
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_emits_logout_audit_event()
    {
        // Arrange
        var logger = new TestLogger<BattleNetLogoutFunction>();
        var fn = MakeFunction(logger);
        var principal = MakePrincipal("bnet-42");
        var ctx = MakeFunctionContext(principal);
        var httpContext = new DefaultHttpContext();
        var req = httpContext.Request;

        // Act
        fn.Run(req, ctx);

        // Assert: logger called with "logout" and "success"
        Assert.Single(logger.Entries, e => e.IsAudit(
            action: "logout",
            actorId: "bnet-42",
            result: "success"));
    }
}
