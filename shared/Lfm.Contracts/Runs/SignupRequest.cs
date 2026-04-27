// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Request body for POST /api/runs/{id}/signup.
/// Mirrors the Zod <c>signupSchema</c> in
/// <c>functions/src/functions/runs-signup.ts</c>.
/// </summary>
public sealed record SignupRequest(
    string? CharacterId,
    string? DesiredAttendance,
    int? SpecId);
