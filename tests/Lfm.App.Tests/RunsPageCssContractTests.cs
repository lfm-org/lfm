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
