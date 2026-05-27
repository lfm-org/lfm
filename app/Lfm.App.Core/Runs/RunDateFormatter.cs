// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;

namespace Lfm.App.Runs;

public static class RunDateFormatter
{
    public static string FormatDisplay(string? isoDate)
    {
        if (string.IsNullOrEmpty(isoDate)) return "\u2014";
        return DateTimeOffset.TryParse(isoDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto.ToString("g", CultureInfo.CurrentCulture)
            : isoDate;
    }
}
