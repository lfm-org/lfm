// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Lfm.Api.Helpers;

namespace Lfm.Api.Runs;

/// <summary>
/// Translates a <see cref="RunOperationResult"/> failure variant into the
/// matching <c>problem+json</c> <see cref="IActionResult"/>. Functions handle
/// the success path themselves (status code 200/201 + custom DTO mapping +
/// success audit emission), so this translator is failure-only.
/// </summary>
public static class RunOperationResultExtensions
{
    public static IActionResult ToProblemResult(this RunOperationResult result, HttpContext httpContext) =>
        result switch
        {
            RunOperationResult.NotFound nf =>
                Problem.NotFound(httpContext, nf.Code, nf.Message),
            RunOperationResult.BadRequest br when br.Errors is not null =>
                Problem.BadRequest(httpContext, br.Code, br.Message,
                    new Dictionary<string, object?> { ["errors"] = br.Errors }),
            RunOperationResult.BadRequest br =>
                Problem.BadRequest(httpContext, br.Code, br.Message),
            RunOperationResult.Forbidden fb =>
                Problem.Forbidden(httpContext, fb.Code, fb.Message),
            RunOperationResult.ConflictResult cf =>
                Problem.Conflict(httpContext, cf.Code, cf.Message),
            RunOperationResult.PreconditionFailed pf =>
                Problem.PreconditionFailed(httpContext, pf.Code, pf.Message),
            RunOperationResult.ServiceUnavailable su =>
                Problem.ServiceUnavailable(httpContext, su.Code, su.Message),
            RunOperationResult.Ok =>
                throw new InvalidOperationException(
                    "RunOperationResult.Ok is a success — translate at the call site, not via ToProblemResult."),
            _ => throw new InvalidOperationException($"Unhandled RunOperationResult: {result.GetType().Name}"),
        };
}
