// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

/// <summary>
/// Locks the .attendance-pill--* background colors against WCAG 2.2 AA.
/// Relates to issue #44 — the prior palette had three variants below the
/// 4.5:1 text-contrast bar (--in 4.25, --late 3.00, --away 4.17) which axe
/// caught at E2E time. These tests catch the regression at unit-test time
/// instead of waiting for the full Docker E2E sweep.
///
/// If you change the colors here, update <c>app/wwwroot/css/app.css</c> to
/// match. The two are intentionally duplicated — the test is the contract.
/// </summary>
public class AttendancePillContrastTests
{
    private const string PillTextColor = "#ffffff";

    // Mirror of .attendance-pill--* in app/wwwroot/css/app.css.
    public static TheoryData<string, string> PillBackgrounds() => new()
    {
        { "--in",    "#1d8049" },
        { "--late",  "#a05900" },
        { "--bench", "#6c757d" },
        { "--out",   "#c0392b" },
        { "--away",  "#ad4400" },
    };

    [Theory]
    [MemberData(nameof(PillBackgrounds))]
    public void Pill_background_meets_WCAG_AA_against_white_text(string variant, string background)
    {
        var ratio = ColorContrast.Ratio(background, PillTextColor);
        Assert.True(
            ratio >= 4.5,
            $"{variant} background {background} on white text has contrast ratio {ratio:F2}:1 — needs ≥ 4.5:1 for WCAG 2.2 AA (text < 18pt / 14pt bold).");
    }
}
