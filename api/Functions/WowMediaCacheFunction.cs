// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Helpers;
using Lfm.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Primitives;

namespace Lfm.Api.Functions;

public class WowMediaCacheFunction(IWowMediaCache mediaCache)
{
    [Function("wow-media-cache")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wow/media/cache")] HttpRequest req,
        CancellationToken ct)
        => ServeAsync(req, ct);

    /// <summary>
    /// <c>/api/v1/wow/media/cache</c> alias for <see cref="Run"/>.
    /// See <c>docs/api-versioning.md</c>.
    /// </summary>
    [Function("wow-media-cache-v1")]
    public Task<IActionResult> RunV1(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/wow/media/cache")] HttpRequest req,
        CancellationToken ct)
        => ServeAsync(req, ct);

    private async Task<IActionResult> ServeAsync(HttpRequest req, CancellationToken ct)
    {
        if (!req.Query.TryGetValue("source", out StringValues encodedSource)
            || StringValues.IsNullOrEmpty(encodedSource))
        {
            return Problem.BadRequest(req.HttpContext, "missing-media-source", "Missing media source.");
        }

        var content = await mediaCache.GetOrFetchAsync(encodedSource.ToString(), ct);
        if (content is null)
            return Problem.NotFound(req.HttpContext, "media-not-found", "Media asset not found.");

        req.HttpContext.Response.Headers.CacheControl = "public, max-age=604800";
        return new FileContentResult(content.Content, content.ContentType);
    }
}
