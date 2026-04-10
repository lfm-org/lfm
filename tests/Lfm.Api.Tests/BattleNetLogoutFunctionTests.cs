using FluentAssertions;
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
    public void Run_clears_auth_cookie_and_redirects_to_app_base_url()
    {
        // Arrange
        var fn = MakeFunction();
        var principal = MakePrincipal();
        var ctx = MakeFunctionContext(principal);
        var httpContext = new DefaultHttpContext();
        var req = httpContext.Request;

        // Act
        var result = fn.Run(req, ctx);

        // Assert: redirects to AppBaseUrl
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Url.Should().Be(AppBaseUrl, "logout should redirect to AppBaseUrl");
        redirect.Permanent.Should().BeFalse("logout redirect must be 302");

        // Assert: auth cookie is cleared (deleted)
        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].ToArray();
        var cookieHeader = setCookieHeaders
            .Where(h => !string.IsNullOrEmpty(h) && h.Contains(FakeCookieName))
            .FirstOrDefault();
        cookieHeader.Should().NotBeNullOrEmpty("the auth cookie must be in the Set-Cookie response headers");
        // Cookie.Delete() sets expires in the past, which is a valid way to delete a cookie
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            (cookieHeader.Contains("max-age=0") || cookieHeader.Contains("expires=Thu, 01 Jan 1970"))
                .Should().BeTrue("the auth cookie must be deleted via MaxAge=0 or past expiry date");
        }
    }

    // Verify that the [RequireAuth] attribute is present on the Run method.
    // AuthPolicyMiddleware enforces the 401 at the framework level based on this attribute;
    // no unit test is needed for the 401 path itself.
    [Fact]
    public void Run_method_has_RequireAuth_attribute()
    {
        var method = typeof(BattleNetLogoutFunction).GetMethod(nameof(BattleNetLogoutFunction.Run));
        method.Should().NotBeNull();
        method!.GetCustomAttributes(typeof(RequireAuthAttribute), inherit: false)
            .Should().HaveCount(1, "BattleNetLogoutFunction.Run must carry [RequireAuth] for AuthPolicyMiddleware to enforce 401");
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
        logger.Entries.Should().ContainSingle(e => e.IsAudit(
            action: "logout",
            actorId: "bnet-42",
            result: "success"),
            "logout must emit a logout audit event with result=success");
    }
}
