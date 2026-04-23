// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Reflection;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Middleware;

/// <summary>
/// Adds standard security response headers, the AGPL §13 source-code notice,
/// and a default <c>Cache-Control: private, no-store</c> (for handlers that do
/// not opt-in to a caching directive) to every HTTP response. Runs early in
/// the pipeline so headers are present even on error responses; the
/// <c>Cache-Control</c> default is applied in a <c>finally</c> block so it is
/// still attached when the downstream pipeline throws.
/// </summary>
public sealed class SecurityHeadersMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly string SourceCommit = ReadSourceCommit();

    private readonly string _sourceRepositoryUrl;

    public SecurityHeadersMiddleware(IOptions<AgplOptions> options)
    {
        _sourceRepositoryUrl = options.Value.SourceRepositoryUrl;
    }

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
        httpContext.Response.Headers.Append("X-Source-Code", _sourceRepositoryUrl);
        httpContext.Response.Headers.Append("X-Source-Commit", SourceCommit);

        try
        {
            await next(context);
        }
        finally
        {
            if (!httpContext.Response.Headers.ContainsKey("Cache-Control"))
            {
                httpContext.Response.Headers.Append("Cache-Control", "private, no-store");
            }
        }
    }

    // Reads the build-time git commit from AssemblyMetadata("GitCommit", ...).
    // Populated by CI via `dotnet build /p:GitCommit=<sha>`. Local builds
    // without the property return "unknown", which is an acceptable AGPL
    // disclosure (the X-Source-Code URL still identifies the repository).
    private static string ReadSourceCommit()
    {
        var attrs = typeof(SecurityHeadersMiddleware).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>();
        foreach (var attr in attrs)
        {
            if (attr.Key == "GitCommit" && !string.IsNullOrEmpty(attr.Value))
            {
                return attr.Value;
            }
        }
        return "unknown";
    }
}
