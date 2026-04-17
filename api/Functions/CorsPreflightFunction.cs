// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Lfm.Api.Functions;

/// <summary>
/// Catch-all OPTIONS handler. The Azure Functions host only routes requests to the
/// worker pipeline when a matching function exists. Without this function, OPTIONS
/// preflight requests are handled by the host itself (with no CORS headers), causing
/// credentialed cross-origin requests from the Blazor WASM app to fail.
///
/// The actual CORS response headers are set by <see cref="Middleware.CorsMiddleware"/>,
/// which runs before this function and short-circuits with 204 for OPTIONS requests.
/// This function exists only so the host routes OPTIONS into the worker pipeline.
/// </summary>
public class CorsPreflightFunction
{
    [Function("cors-preflight")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*path}")] HttpRequest req)
    {
        // CorsMiddleware already handled the response; this is a fallback.
        return new StatusCodeResult(StatusCodes.Status204NoContent);
    }
}
