// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Lfm.App.Components;
using Lfm.Contracts.Runs;
using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.App.Tests;

public class AttendanceRosterSectionTests : ComponentTestBase
{
    private static RunCharacterDto MakeCharacter(
        string name = "TestChar",
        int classId = 1,
        string className = "Warrior",
        string realm = "Area-52",
        string role = "DPS",
        string spec = "Arms",
        string desired = "IN",
        string reviewed = "IN") =>
        new(
            Id: "rc-1",
            CharacterId: "char-1",
            CharacterName: name,
            CharacterRealm: realm,
            CharacterLevel: 80,
            CharacterClassId: classId,
            CharacterClassName: className,
            CharacterRaceId: 1,
            CharacterRaceName: "Human",
            DesiredAttendance: desired,
            ReviewedAttendance: reviewed,
            SpecId: 71,
            SpecName: spec,
            Role: role,
            IsCurrentUser: false);

    [Fact]
    public void Renders_Heading_With_Group_Label_And_Count()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeCharacter(name: "Warrior1"),
            MakeCharacter(name: "Warrior2"),
        };

        var cut = Render<AttendanceRosterSection>(p => p
            .Add(x => x.GroupLabel, "IN")
            .Add(x => x.Characters, chars));

        Assert.Contains("IN (2)", cut.Markup);
    }

    [Fact]
    public void Renders_Character_Rows_With_Class_Colored_Names()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeCharacter(name: "Thrall", classId: 7, className: "Shaman"),
        };

        var cut = Render<AttendanceRosterSection>(p => p
            .Add(x => x.GroupLabel, "IN")
            .Add(x => x.Characters, chars));

        // WowClassBadge renders a span with class color
        var span = cut.Find("span[style]");
        Assert.Equal("Thrall", span.TextContent);
        Assert.Contains($"color:{WowClasses.GetColor(7)}", span.GetAttribute("style"));
    }

    [Fact]
    public void Shows_Desired_Attendance_Column_When_Enabled()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeCharacter(name: "Jaina", desired: "BENCH"),
        };

        var cut = Render<AttendanceRosterSection>(p => p
            .Add(x => x.GroupLabel, "IN")
            .Add(x => x.Characters, chars)
            .Add(x => x.ShowDesiredAttendance, true));

        Assert.Contains("Desired", cut.Markup);
        Assert.Contains("BENCH", cut.Markup);
    }

    [Fact]
    public void Hides_Desired_Attendance_Column_By_Default()
    {
        var chars = new List<RunCharacterDto>
        {
            MakeCharacter(name: "Jaina"),
        };

        var cut = Render<AttendanceRosterSection>(p => p
            .Add(x => x.GroupLabel, "IN")
            .Add(x => x.Characters, chars));

        Assert.DoesNotContain("Desired", cut.Markup);
    }
}
