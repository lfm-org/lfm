// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Globalization;
using Lfm.App.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunDateFormatterTests
{
    [Fact]
    public void FormatDisplay_Uses_Current_Culture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = CultureInfo.GetCultureInfo("fi-FI");
            CultureInfo.CurrentCulture = culture;
            var start = new DateTimeOffset(2026, 5, 20, 15, 30, 0, TimeSpan.Zero);

            var result = RunDateFormatter.FormatDisplay(start.ToString("o", CultureInfo.InvariantCulture));

            Assert.Equal(start.ToString("g", culture), result);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
