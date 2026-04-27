// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for POST /api/runs.
/// </summary>
public sealed record CreateRunRequest(
    string? StartTime,
    string? SignupCloseTime,
    string? Description,
    string? Visibility,
    int? InstanceId,
    string? InstanceName,
    string? Difficulty,
    int? Size,
    int? KeystoneLevel = null);
