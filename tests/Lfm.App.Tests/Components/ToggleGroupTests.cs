// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Lfm.App.Components;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace Lfm.App.Tests.Components;

/// <summary>
/// bUnit coverage for the generic ToggleGroup segmented control.
/// Keyboard spec follows WAI-ARIA Authoring Practices for the radiogroup
/// pattern; every assertion pins a WCAG 2.2 AA-relevant behaviour.
/// </summary>
public class ToggleGroupTests : ComponentTestBase
{
    private static readonly (string Value, string Label)[] ThreeOptions = new[]
    {
        ("RAID", "Raid"),
        ("DUNGEON", "Dungeon"),
        ("OTHER", "Other"),
    };

    // ── Rendering ────────────────────────────────────────────────────────────

    [Fact]
    public void Renders_one_radio_button_per_option_inside_a_radiogroup()
    {
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.AriaLabel, "Activity"));

        var group = cut.Find("[role='radiogroup']");
        Assert.Equal("Activity", group.GetAttribute("aria-label"));

        var buttons = cut.FindAll("[role='radio']");
        Assert.Equal(3, buttons.Count);
        Assert.Equal("Raid", buttons[0].TextContent.Trim());
        Assert.Equal("Dungeon", buttons[1].TextContent.Trim());
        Assert.Equal("Other", buttons[2].TextContent.Trim());
    }

    [Fact]
    public void Selected_button_has_aria_checked_true_and_tabindex_0()
    {
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "DUNGEON")
            .Add(c => c.AriaLabel, "Activity"));

        var buttons = cut.FindAll("[role='radio']");
        Assert.Equal("false", buttons[0].GetAttribute("aria-checked"));
        Assert.Equal("true", buttons[1].GetAttribute("aria-checked"));
        Assert.Equal("false", buttons[2].GetAttribute("aria-checked"));
        Assert.Equal("-1", buttons[0].GetAttribute("tabindex"));
        Assert.Equal("0", buttons[1].GetAttribute("tabindex"));
        Assert.Equal("-1", buttons[2].GetAttribute("tabindex"));
    }

    [Fact]
    public void AriaLabelledBy_is_preferred_over_AriaLabel_when_both_provided()
    {
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.AriaLabel, "fallback")
            .Add(c => c.AriaLabelledBy, "visible-label-id"));

        var group = cut.Find("[role='radiogroup']");
        Assert.Equal("visible-label-id", group.GetAttribute("aria-labelledby"));
        // Both attributes render; AT will prefer labelledby over label per ARIA.
        Assert.Equal("fallback", group.GetAttribute("aria-label"));
    }

    // ── Click selection ──────────────────────────────────────────────────────

    [Fact]
    public void Clicking_an_unselected_option_fires_ValueChanged_with_its_value()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, v => received = v));

        cut.FindAll("[role='radio']")[1].Click();

        Assert.Equal("DUNGEON", received);
    }

    [Fact]
    public void Clicking_the_already_selected_option_does_not_fire_ValueChanged()
    {
        var callbackCount = 0;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, _ => callbackCount++));

        cut.FindAll("[role='radio']")[0].Click();

        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void Disabled_group_does_not_fire_ValueChanged_on_click()
    {
        var callbackCount = 0;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.Disabled, true)
            .Add(c => c.ValueChanged, _ => callbackCount++));

        var buttons = cut.FindAll("[role='radio']");

        // The SUT's SelectAsync has an explicit `if (Disabled) return;`
        // guard, so the callback is not invoked even if bUnit's synthetic
        // click event bypasses the native <button disabled> suppression.
        buttons[1].Click();

        Assert.Equal(0, callbackCount);
        // And the native disabled attribute is still present on every
        // option so the browser itself suppresses real click events.
        Assert.NotNull(buttons[0].GetAttribute("disabled"));
        Assert.NotNull(buttons[1].GetAttribute("disabled"));
        Assert.NotNull(buttons[2].GetAttribute("disabled"));
    }

    // ── Keyboard navigation (WAI-ARIA radiogroup) ────────────────────────────

    [Fact]
    public void ArrowRight_moves_selection_to_next()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "DUNGEON")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("OTHER", received);
    }

    [Fact]
    public void ArrowRight_wraps_from_last_to_first()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "OTHER")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("RAID", received);
    }

    [Fact]
    public void ArrowLeft_moves_selection_to_previous_and_wraps_at_start()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("OTHER", received); // wrapped
    }

    [Theory]
    [InlineData("ArrowDown", "DUNGEON")]
    [InlineData("ArrowUp", "OTHER")]
    public void ArrowDown_and_ArrowUp_behave_like_Right_and_Left(string key, string expected)
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(expected, received);
    }

    [Fact]
    public void IsRtl_swaps_ArrowRight_and_ArrowLeft_to_follow_reading_direction()
    {
        // Under dir="rtl", flex-direction: row renders Options[0] on the
        // visual RIGHT. ArrowRight should therefore retreat (move toward
        // index 0) and ArrowLeft should advance — matching the reader's
        // RTL reading order.
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "DUNGEON")
            .Add(c => c.IsRtl, true)
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("RAID", received); // RTL: ArrowRight retreats

        received = null;
        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("OTHER", received); // RTL: ArrowLeft advances
    }

    [Fact]
    public void IsRtl_does_not_affect_ArrowUp_or_ArrowDown()
    {
        // Block-direction keys map to vertical layout, not inline, so
        // they stay index-forward/back regardless of RTL.
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.IsRtl, true)
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("DUNGEON", received);
    }

    [Fact]
    public void Home_jumps_to_first_option()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "OTHER")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "Home" });

        Assert.Equal("RAID", received);
    }

    [Fact]
    public void End_jumps_to_last_option()
    {
        string? received = null;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, v => received = v));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = "End" });

        Assert.Equal("OTHER", received);
    }

    [Fact]
    public void Space_on_already_selected_option_is_a_no_op()
    {
        var callbackCount = 0;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, _ => callbackCount++));

        cut.Find("[role='radiogroup']")
           .KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Equal(0, callbackCount);
    }

    [Fact]
    public void Unrelated_keys_do_not_fire_ValueChanged()
    {
        var callbackCount = 0;
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "RAID")
            .Add(c => c.ValueChanged, _ => callbackCount++));

        foreach (var key in new[] { "Enter", "a", "Tab", "Escape" })
        {
            cut.Find("[role='radiogroup']")
               .KeyDown(new KeyboardEventArgs { Key = key });
        }

        Assert.Equal(0, callbackCount);
    }

    // ── CSS hook for the selected state (drives forced-colors CSS too) ───────

    [Fact]
    public void Selected_option_carries_data_selected_true_for_CSS_targeting()
    {
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, ThreeOptions)
            .Add(c => c.Value, "DUNGEON"));

        var buttons = cut.FindAll("[role='radio']");
        Assert.Equal("false", buttons[0].GetAttribute("data-selected"));
        Assert.Equal("true", buttons[1].GetAttribute("data-selected"));
        Assert.Equal("false", buttons[2].GetAttribute("data-selected"));
    }

    // ── Empty options — renders nothing but doesn't throw ────────────────────

    [Fact]
    public void Empty_Options_renders_an_empty_radiogroup()
    {
        var cut = Render<ToggleGroup<string>>(p => p
            .Add(c => c.Options, Array.Empty<(string, string)>())
            .Add(c => c.Value, "RAID")
            .Add(c => c.AriaLabel, "Empty"));

        var group = cut.Find("[role='radiogroup']");
        Assert.Empty(cut.FindAll("[role='radio']"));
        Assert.Equal("Empty", group.GetAttribute("aria-label"));
    }
}
