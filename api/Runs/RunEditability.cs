// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Runs;

/// <summary>
/// Shared editability check for runs. Mirrors isEditingClosed in run-editability.ts.
/// Returns true when signupCloseTime or startTime has already passed.
/// </summary>
public static class RunEditability
{
    public static bool IsEditingClosed(string? signupCloseTime, string? startTime, DateTimeOffset now)
    {
        if (!string.IsNullOrEmpty(signupCloseTime)
            && DateTimeOffset.TryParse(signupCloseTime, out var closeTime)
            && closeTime <= now)
            return true;

        if (!string.IsNullOrEmpty(startTime)
            && DateTimeOffset.TryParse(startTime, out var start)
            && start <= now)
            return true;

        return false;
    }
}
