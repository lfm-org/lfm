// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

/// <summary>
/// Locks the .difficulty-pill--* theme-specific colors against WCAG 2.2 AA
/// 1.4.3 (text contrast ≥ 4.5:1) on the two FluentCard layer backgrounds.
/// Relates to issue #28. WoW's canonical #ff8000 (mythic) / #a335ee (heroic)
/// fail one theme each, so the CSS uses light-dark() pairs with a darkened
/// mythic for light theme and a lightened heroic for dark theme.
///
/// Background samples reflect the FluentUI v2 neutral-layer-1 defaults that
/// FluentCard renders on top of. If you change the CSS colors, update this
/// test — the test is the contract, the CSS follows.
/// </summary>
public class DifficultyPillContrastTests
{
    // FluentUI Web Components v2 neutral-layer-1 defaults.
    private const string LightLayer = "#f9f9f9";
    private const string DarkLayer = "#1f1f1f";

    // Mirror of the light-dark() pairs in app/wwwroot/css/app.css.
    private const string MythicLight = "#b35900";
    private const string MythicDark = "#ff8000";
    private const string HeroicLight = "#a335ee";
    private const string HeroicDark = "#c77dff";

    public static TheoryData<string, string, string> PillOnLayer() => new()
    {
        { "mythic on light layer", MythicLight, LightLayer },
        { "mythic on dark layer",  MythicDark,  DarkLayer },
        { "heroic on light layer", HeroicLight, LightLayer },
        { "heroic on dark layer",  HeroicDark,  DarkLayer },
    };

    [Theory]
    [MemberData(nameof(PillOnLayer))]
    public void Pill_text_meets_WCAG_AA_against_layer(string label, string foreground, string background)
    {
        var ratio = ColorContrast.Ratio(foreground, background);
        Assert.True(
            ratio >= 4.5,
            $"{label}: {foreground} on {background} has contrast ratio {ratio:F2}:1 — needs ≥ 4.5:1 for WCAG 2.2 AA.");
    }
}
