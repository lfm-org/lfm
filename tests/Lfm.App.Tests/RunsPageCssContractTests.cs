// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Xunit;

namespace Lfm.App.Tests;

public class RunsPageCssContractTests
{
    [Fact]
    public void Desktop_workbench_gives_signup_rail_room_for_one_row_attendance_toggle()
    {
        var css = File.ReadAllText(FindRepoFile("app", "Pages", "RunsPage.razor.css"));

        Assert.Contains(
            "grid-template-columns: minmax(0, 1fr) clamp(23.5rem, 27vw, 25rem);",
            css);
    }

    [Fact]
    public void Signup_attendance_toggle_fills_available_panel_width()
    {
        var css = File.ReadAllText(FindRepoFile("app", "Components", "Runs", "RunSignupPanel.razor.css"));

        Assert.Contains("::deep .run-signup-attendance-toggle", css);
        Assert.Contains("inline-size: 100%;", css);
        Assert.Contains("::deep .run-signup-attendance-toggle .toggle-group__option", css);
        Assert.Contains("flex: 1 1 0;", css);
    }

    [Fact]
    public void Wide_roster_uses_matching_three_column_grids_for_attending_and_not_attending()
    {
        var css = File.ReadAllText(FindRepoFile("app", "Pages", "RunsPage.razor.css"));

        Assert.Contains("@media (min-width: 88em)", css);
        Assert.Contains(".roster-role-grid,\n    .roster-rows", css);
        Assert.Contains("grid-template-columns: repeat(3, minmax(0, 1fr));", css);
    }

    [Fact]
    public void Roster_role_icons_use_single_color_outline_styling()
    {
        var css = File.ReadAllText(FindRepoFile("app", "Pages", "RunsPage.razor.css"));

        Assert.Contains("stroke: currentColor;", css);
        Assert.Contains("fill: none;", css);
        Assert.DoesNotContain(".roster-role-title__icon--dps .roster-role-title__icon-blade", css);
        Assert.DoesNotContain(".roster-role-title__icon--dps .roster-role-title__icon-edge", css);
        Assert.DoesNotContain("rgb(255 255 255", css);
    }

    [Fact]
    public void Character_row_places_name_attendance_and_icons_in_requested_card_rows()
    {
        var css = File.ReadAllText(FindRepoFile("app", "wwwroot", "css", "app.css"));

        Assert.Contains("grid-template-areas:", css);
        Assert.Contains("\"name name\"", css);
        Assert.Contains("\". attendance\"", css);
        Assert.Contains("\"icons .\"", css);
        Assert.Contains("grid-area: name;", css);
        Assert.Contains("grid-area: attendance;", css);
        Assert.Contains("grid-area: icons;", css);
    }

    [Fact]
    public void Run_list_composition_chips_are_muted_indicators_not_alerts()
    {
        var css = File.ReadAllText(FindRepoFile("app", "Components", "RunListItem.razor.css"));

        Assert.Contains(".run-list-item__roleicon", css);
        Assert.Contains(".run-list-item__roleslot--tank", css);
        Assert.Contains(".run-list-item__roleslot--healer", css);
        Assert.Contains(".run-list-item__roleslot--dps", css);
        Assert.DoesNotContain(".run-list-item__roleslot--short", css);
        Assert.DoesNotContain("#c0392b", css);
    }

    private static string FindRepoFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file {Path.Combine(segments)}.");
    }
}
