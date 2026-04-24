// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using System.Text.Json;
using Lfm.Api.Helpers;
using Lfm.Api.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Options;

namespace Lfm.Api.Middleware;

/// <summary>
/// Rejects requests whose advertised <c>Content-Length</c> exceeds
/// <see cref="RequestSizeLimitOptions.MaxBytes"/> with 413 Payload Too Large
/// + RFC 9457 <c>application/problem+json</c>. Runs early (before auth and
/// the function body) so we do not burn compute on payloads the API would
/// reject anyway.
///
/// <para>
/// Requests without a <c>Content-Length</c> header (GET, DELETE, chunked
/// uploads) bypass the guard. Chunked bodies are not on the API surface
/// today — if that changes, a later slice can add a streaming cap that
/// counts bytes as they arrive.
/// </para>
/// </summary>
public sealed class RequestSizeLimitMiddleware : IFunctionsWorkerMiddleware
{
    private readonly RequestSizeLimitOptions _options;

    public RequestSizeLimitMiddleware(IOptions<RequestSizeLimitOptions> options)
    {
        _options = options.Value;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        if (httpContext.Request.ContentLength is long length && length > _options.MaxBytes)
        {
            await WriteTooLargeAsync(httpContext, length);
            return;
        }

        await next(context);
    }

    private async Task WriteTooLargeAsync(HttpContext httpContext, long advertisedLength)
    {
        var problem = new ProblemDetails
        {
            Type = $"{Problem.TypeBase}#payload-too-large",
            Title = "Payload Too Large",
            Status = StatusCodes.Status413PayloadTooLarge,
            Detail = $"Request body exceeds the {_options.MaxBytes}-byte cap (got {advertisedLength}).",
            Instance = httpContext.Request.Path.Value,
        };
        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrEmpty(traceId))
            problem.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        httpContext.Response.ContentType = Problem.ContentType;
        await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem);
    }
}
