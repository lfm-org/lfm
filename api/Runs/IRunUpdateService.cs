// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

/// <summary>
/// Run-update policy lifted out of <c>RunsUpdateFunction</c>. The Function
/// adapter handles HTTP body deserialization, distinguishing omitted fields
/// from explicit nulls (the <see cref="RunUpdatePresentFields"/> projection),
/// request validation, and the translation of this service's
/// <see cref="RunOperationResult"/> into <c>problem+json</c> responses or 200.
/// </summary>
public interface IRunUpdateService
{
    Task<RunOperationResult> UpdateAsync(
        string runId,
        UpdateRunRequest body,
        RunUpdatePresentFields presentFields,
        string ifMatchEtag,
        SessionPrincipal principal,
        CancellationToken ct);
}

/// <summary>
/// Reports which fields were present in the original PUT body (versus omitted)
/// so the service can distinguish "leave field alone" from "explicit null /
/// clear field". Computed by the Function from the parsed
/// <see cref="System.Text.Json.JsonDocument"/> before calling the service —
/// the service never sees the raw JSON.
/// </summary>
public sealed record RunUpdatePresentFields(
    bool StartTime,
    bool SignupCloseTime,
    bool Description,
    bool Visibility,
    bool InstanceId,
    bool Difficulty,
    bool Size,
    bool KeystoneLevel);
