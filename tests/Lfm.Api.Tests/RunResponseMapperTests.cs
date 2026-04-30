// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Mappers;
using Lfm.Api.Repositories;
using Xunit;

namespace Lfm.Api.Tests;

public class RunResponseMapperTests
{
    [Fact]
    public void ToDetail_projects_run_document_without_exposing_raider_ids()
    {
        var doc = MakeRunDoc(runCharacters: [
            MakeCharacterEntry("entry-own", "Ownchar", "bnet-current"),
            MakeCharacterEntry("entry-peer", "Peerchar", "bnet-peer")
        ]);

        var dto = RunResponseMapper.ToDetail(doc, "bnet-current");

        Assert.Equal("run-1", dto.Id);
        Assert.Equal("HEROIC", dto.Difficulty);
        Assert.Equal(30, dto.Size);
        Assert.Equal(2, dto.RunCharacters.Count);

        var own = Assert.Single(dto.RunCharacters, c => c.CharacterName == "Ownchar");
        Assert.True(own.IsCurrentUser);

        var peer = Assert.Single(dto.RunCharacters, c => c.CharacterName == "Peerchar");
        Assert.False(peer.IsCurrentUser);
    }

    [Fact]
    public void ToSummary_projects_same_sanitized_roster_as_detail()
    {
        var doc = MakeRunDoc(runCharacters: [
            MakeCharacterEntry("entry-own", "Ownchar", "bnet-current"),
            MakeCharacterEntry("entry-peer", "Peerchar", "bnet-peer")
        ]);

        var dto = RunResponseMapper.ToSummary(doc, "bnet-current");

        Assert.Equal("run-1", dto.Id);
        Assert.Equal("HEROIC", dto.Difficulty);
        Assert.Equal(30, dto.Size);
        Assert.Equal(2, dto.RunCharacters.Count);
        Assert.True(dto.RunCharacters.Single(c => c.CharacterName == "Ownchar").IsCurrentUser);
        Assert.False(dto.RunCharacters.Single(c => c.CharacterName == "Peerchar").IsCurrentUser);
    }

    private static RunDocument MakeRunDoc(IReadOnlyList<RunCharacterEntry> runCharacters) =>
        new(
            Id: "run-1",
            StartTime: "2026-06-01T19:00:00Z",
            SignupCloseTime: "2026-06-01T18:30:00Z",
            Description: "Test run",
            ModeKey: "NORMAL:20",
            Visibility: "GUILD",
            CreatorGuild: "Test Guild",
            CreatorGuildId: 12345,
            InstanceId: 42,
            InstanceName: "Test Instance",
            CreatorBattleNetId: "bnet-creator",
            CreatedAt: "2026-05-01T00:00:00Z",
            Ttl: 86400,
            RunCharacters: runCharacters,
            Difficulty: "HEROIC",
            Size: 30,
            KeystoneLevel: null);

    private static RunCharacterEntry MakeCharacterEntry(string id, string name, string raiderBattleNetId) =>
        new(
            Id: id,
            CharacterId: id,
            CharacterName: name,
            CharacterRealm: "silvermoon",
            CharacterLevel: 80,
            CharacterClassId: 2,
            CharacterClassName: "Paladin",
            CharacterRaceId: 1,
            CharacterRaceName: "Human",
            RaiderBattleNetId: raiderBattleNetId,
            DesiredAttendance: "IN",
            ReviewedAttendance: "IN",
            SpecId: 65,
            SpecName: "Holy",
            Role: "HEALER");
}
