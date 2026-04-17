// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace Lfm.Api.Middleware;

/// <summary>
/// Adds standard security response headers to every HTTP response.
/// Runs early in the pipeline so headers are present even on error responses.
/// </summary>
public sealed class SecurityHeadersMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        httpContext.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        httpContext.Response.Headers.Append("X-Frame-Options", "DENY");
        httpContext.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        httpContext.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        httpContext.Response.Headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

        await next(context);
    }
}
