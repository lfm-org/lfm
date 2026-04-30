// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Runs;

/// <summary>
/// Discriminated result returned by the run-* services
/// (<c>IRunCreateService</c>, <c>IRunUpdateService</c>, <c>IRunSignupService</c>).
/// Functions translate each variant to the appropriate <c>problem+json</c>
/// response or 200/201.
/// </summary>
public abstract record RunOperationResult
{
    public sealed record Ok(RunDocument Run) : RunOperationResult;
    public sealed record NotFound(string Code, string Message) : RunOperationResult;
    public sealed record BadRequest(string Code, string Message, IReadOnlyList<string>? Errors = null) : RunOperationResult;
    public sealed record Forbidden(string Code, string Message, string AuditReason) : RunOperationResult;
    // Suffixed "Result" to avoid shadowing Microsoft.AspNetCore.Mvc.ConflictResult
    // at Function call sites that use both namespaces.
    public sealed record ConflictResult(string Code, string Message) : RunOperationResult;
    public sealed record PreconditionFailed(string Code, string Message) : RunOperationResult;
    public sealed record ServiceUnavailable(string Code, string Message) : RunOperationResult;
}
