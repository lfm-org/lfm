// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Middleware;

/// <summary>
/// Adds CORS headers to HTTP responses. Must run before AuthPolicyMiddleware so
/// that preflight OPTIONS requests are not blocked by auth checks.
/// </summary>
public sealed class CorsMiddleware(IOptions<CorsOptions> corsOpts) : IFunctionsWorkerMiddleware
{
    private readonly HashSet<string> _allowedOrigins =
        new(corsOpts.Value.AllowedOrigins, StringComparer.OrdinalIgnoreCase);

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var origin = httpContext.Request.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && _allowedOrigins.Contains(origin))
        {
            httpContext.Response.Headers.Append("Access-Control-Allow-Origin", origin);
            httpContext.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "ETag");
            httpContext.Response.Headers.Append("Vary", "Origin");
        }

        // Handle preflight
        if (httpContext.Request.Method == HttpMethods.Options)
        {
            var requestedMethod = httpContext.Request.Headers["Access-Control-Request-Method"].ToString();
            var requestedHeaders = httpContext.Request.Headers["Access-Control-Request-Headers"].ToString();

            if (!string.IsNullOrEmpty(requestedMethod))
            {
                httpContext.Response.Headers.Append("Access-Control-Allow-Methods", requestedMethod);
            }

            if (!string.IsNullOrEmpty(requestedHeaders))
            {
                httpContext.Response.Headers.Append("Access-Control-Allow-Headers", requestedHeaders);
            }

            httpContext.Response.Headers.Append("Access-Control-Max-Age", "3600");
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return; // Short-circuit — do not run the function
        }

        await next(context);
    }
}
