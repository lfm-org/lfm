// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Bunit;
using Lfm.App.Components;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.App.Tests;

public class CharacterRowTests : ComponentTestBase
{
    [Fact]
    public void Renders_Name_Attendance_And_Icons_In_Card_Order()
    {
        var cut = Render<CharacterRow>(p => p
            .Add(x => x.Character, MakeCharacter()));

        var row = cut.Find(".character-row");
        var children = row.Children.ToArray();

        Assert.Contains("Warlock \u00B7 Demonology", row.GetAttribute("aria-label") ?? "");
        Assert.Equal(3, children.Length);
        Assert.Contains("character-row__name", children[0].ClassName ?? "");
        Assert.Contains("Shalena", children[0].TextContent);
        Assert.Contains("attendance-pill", children[1].ClassName ?? "");
        Assert.Contains(Loc("runs.attendance.in"), children[1].TextContent);
        Assert.Contains("character-row__icons", children[2].ClassName ?? "");
        Assert.Equal(2, children[2].QuerySelectorAll(".spec-icon").Length);
        Assert.Null(row.QuerySelector(".character-row__main"));
        Assert.Null(row.QuerySelector(".character-row__sub"));
    }

    private static RunCharacterDto MakeCharacter() =>
        new(
            CharacterId: "eu-silvermoon-shalena",
            CharacterName: "Shalena",
            CharacterRealm: "Silvermoon",
            CharacterClassId: 9,
            CharacterClassName: "Warlock",
            DesiredAttendance: "IN",
            ReviewedAttendance: "IN",
            SpecId: 266,
            SpecName: "Demonology",
            Role: "DPS",
            IsCurrentUser: false);
}
