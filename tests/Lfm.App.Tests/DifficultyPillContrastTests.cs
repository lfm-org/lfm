// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

/// <summary>
/// Locks the .difficulty-pill--{mythic,mplus,heroic} color + background pairs against
/// WCAG 2.2 AA 1.4.3 (text contrast ≥ 4.5:1). Relates to issue #28.
///
/// WoW difficulty colors are brand-canonical and cannot be altered. To clear
/// 4.5:1 they sit on the pill *background*, with a neutral text color:
///   - mythic #ff8000 carries black text (8.36:1).
///   - mplus (M+ / mythic keystone) reuses the mythic orange/black pair.
///   - heroic #a335ee carries white text (4.88:1).
///
/// If you change the CSS pairs, update this test — the test is the contract,
/// the CSS follows.
/// </summary>
public class DifficultyPillContrastTests
{
    public static TheoryData<string, string, string> PillTextOnBackground() => new()
    {
        { "mythic", "#000000", "#ff8000" },
        { "mplus", "#000000", "#ff8000" },
        { "heroic", "#ffffff", "#a335ee" },
    };

    [Theory]
    [MemberData(nameof(PillTextOnBackground))]
    public void Pill_text_meets_WCAG_AA_against_brand_background(string variant, string text, string background)
    {
        var ratio = ColorContrast.Ratio(text, background);
        Assert.True(
            ratio >= 4.5,
            $"{variant}: {text} text on {background} background has contrast ratio {ratio:F2}:1 — needs ≥ 4.5:1 for WCAG 2.2 AA.");
    }
}
