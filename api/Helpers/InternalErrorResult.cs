// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Helpers;

/// <summary>
/// Builds a generic 500 response that logs the full exception server-side and
/// exposes only the Functions invocation id as a correlation token to the
/// client. Use in catch-all blocks where the exception source (Cosmos, DPAPI,
/// network clients) could otherwise leak infrastructure details through
/// <c>ex.Message</c> or <c>ex.GetType().Name</c>.
///
/// The response body is RFC 9457 <c>application/problem+json</c> with a stable
/// <c>type</c> URI of <c>https://github.com/lfm-org/lfm/errors#internal-error</c>
/// and <c>extensions.correlationId</c> set to the Functions invocation id for
/// server-side triage. When an <see cref="Activity"/> is current its
/// <c>traceId</c> is attached as a separate extension.
/// </summary>
public static class InternalErrorResult
{
    public static IActionResult Create(ILogger logger, FunctionContext ctx, Exception ex, string operation)
    {
        logger.LogError(
            ex,
            "Unhandled exception in {Operation} (invocation {InvocationId})",
            operation,
            ctx.InvocationId);

        var problem = new ProblemDetails
        {
            Type = $"{Problem.TypeBase}#internal-error",
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected server error occurred.",
            Instance = ctx.GetHttpContext()?.Request.Path.Value,
        };
        problem.Extensions["correlationId"] = ctx.InvocationId;
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
        {
            problem.Extensions["traceId"] = traceId;
        }

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            ContentTypes = { Problem.ContentType },
        };
    }
}
