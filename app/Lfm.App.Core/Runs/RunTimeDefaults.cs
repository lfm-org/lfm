// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.App.Runs;

/// <summary>
/// Time-of-day defaults for the create-run form. Pure so bUnit and unit
/// tests can pin the output without reaching for real <see cref="DateTime.Now"/>.
/// </summary>
public static class RunTimeDefaults
{
    /// <summary>
    /// Returns the next Thursday at 20:00 local wall-clock time, strictly in
    /// the future relative to <paramref name="now"/>. Used as the initial
    /// StartTime on CreateRunPage — raid nights overwhelmingly happen on
    /// Thursday evenings in the target communities.
    /// </summary>
    public static DateTime NextThursday20(DateTime now)
    {
        // DayOfWeek: Sunday=0 … Thursday=4.
        var daysUntilThursday = ((int)DayOfWeek.Thursday - (int)now.DayOfWeek + 7) % 7;
        var candidate = now.Date.AddDays(daysUntilThursday).AddHours(20);
        if (candidate <= now)
            candidate = candidate.AddDays(7);
        return candidate;
    }
}
