// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for PATCH/PUT /api/runs/{id}.
/// All fields are optional; only supplied fields are applied to the existing run.
/// </summary>
public sealed record UpdateRunRequest(
    string? StartTime,
    string? SignupCloseTime,
    string? Description,
    string? Visibility,
    int? InstanceId,
    string? InstanceName,
    string? Difficulty = null,
    int? Size = null,
    int? KeystoneLevel = null);
