// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Run-signup policy lifted out of <c>RunsSignupFunction</c>. The Function
/// adapter handles HTTP body deserialization, request validation, and the
/// translation of this service's <see cref="RunOperationResult"/> into
/// <c>problem+json</c> responses or 200 OK. Audit emission for the success
/// and forbidden paths stays at the Function — same pattern as
/// <see cref="IRunCreateService"/> and <see cref="IRunUpdateService"/>.
/// </summary>
public interface IRunSignupService
{
    Task<RunOperationResult> SignupAsync(
        string runId,
        SignupRequest body,
        SessionPrincipal principal,
        CancellationToken ct);
}
