// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;
using Lfm.Api.Auth;
using Lfm.Api.Options;

namespace Lfm.Api.Middleware;

public sealed class AuthMiddleware(ISessionCipher cipher, IOptions<AuthOptions> authOpts) : IFunctionsWorkerMiddleware
{
    // Small grace window on session-expiry checks. Functions instances read
    // UtcNow from the same kernel source, but requests can transit multiple
    // instances (cookie issued on A, validated on B) with brief clock drift.
    // Matches the default ClockSkew of ASP.NET Core's JWT handler.
    internal static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly AuthOptions _auth = authOpts.Value;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is not null &&
            httpContext.Request.Cookies.TryGetValue(_auth.CookieName, out var cookieValue) &&
            !string.IsNullOrEmpty(cookieValue))
        {
            var principal = cipher.Unprotect(cookieValue);
            if (principal is not null && principal.ExpiresAt + ClockSkew > DateTimeOffset.UtcNow)
            {
                context.Items[SessionKeys.Principal] = principal;
            }
        }
        await next(context);
    }
}
