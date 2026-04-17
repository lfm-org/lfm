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
    private readonly AuthOptions _auth = authOpts.Value;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is not null &&
            httpContext.Request.Cookies.TryGetValue(_auth.CookieName, out var cookieValue) &&
            !string.IsNullOrEmpty(cookieValue))
        {
            var principal = cipher.Unprotect(cookieValue);
            if (principal is not null && principal.ExpiresAt > DateTimeOffset.UtcNow)
            {
                context.Items[SessionKeys.Principal] = principal;
            }
        }
        await next(context);
    }
}
