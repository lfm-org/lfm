// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

public class ColorContrastTests
{
    // Reference values from the WCAG calculator
    // (https://webaim.org/resources/contrastchecker/) so a typo in the
    // luminance math produces a real test failure, not a self-consistent lie.
    [Theory]
    [InlineData("#000000", "#ffffff", 21.00)]
    [InlineData("#ffffff", "#000000", 21.00)] // order independent
    [InlineData("#777777", "#ffffff", 4.48)]
    [InlineData("#2e8b57", "#ffffff", 4.25)]  // old --in (failing)
    [InlineData("#1d8049", "#ffffff", 4.96)]  // new --in (passing)
    public void Ratio_matches_WCAG_calculator_to_two_decimals(string a, string b, double expected)
    {
        var actual = ColorContrast.Ratio(a, b);
        Assert.Equal(expected, actual, 2);
    }

    [Theory]
    [InlineData("#000000", 0.0)]
    [InlineData("#ffffff", 1.0)]
    [InlineData("#808080", 0.2159)]
    public void RelativeLuminance_matches_known_values(string hex, double expected)
    {
        var actual = ColorContrast.RelativeLuminance(hex);
        Assert.Equal(expected, actual, 4);
    }

    [Theory]
    [InlineData("#fff")]
    [InlineData("ffffffff")]
    [InlineData("not-a-color")]
    public void Ratio_throws_on_invalid_hex(string bad)
    {
        Assert.Throws<ArgumentException>(() => ColorContrast.Ratio(bad, "#000000"));
    }
}
