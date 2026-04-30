// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Run-create policy lifted out of <c>RunsCreateFunction</c>. The Function
/// adapter handles HTTP body deserialization, request validation, and the
/// translation of this service's <see cref="RunOperationResult"/> into
/// <c>problem+json</c> responses or 201 Created.
/// </summary>
public interface IRunCreateService
{
    Task<RunOperationResult> CreateAsync(
        CreateRunRequest body,
        SessionPrincipal principal,
        CancellationToken ct);
}
