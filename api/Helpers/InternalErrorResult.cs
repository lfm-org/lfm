// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

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
/// Response body: <c>{ "error": "internal error", "correlationId": "&lt;invocation-id&gt;" }</c>.
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

        return new ObjectResult(new { error = "internal error", correlationId = ctx.InvocationId })
        {
            StatusCode = 500,
        };
    }
}
