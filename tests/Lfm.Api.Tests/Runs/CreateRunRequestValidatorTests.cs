// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests.Runs;

/// <summary>
/// Pins the Mythic+ validation rules on the create-run payload:
///   - Difficulty + Size are both required.
///   - InstanceId is required unless Difficulty == MYTHIC_KEYSTONE.
///   - KeystoneLevel is only valid on Mythic+ and must be present when the
///     run is dungeon-less.
/// </summary>
public class CreateRunRequestValidatorTests
{
    private const string ValidStart = "2026-05-01T20:00:00Z";
    private static readonly CreateRunRequestValidator Sut = new();

    private static CreateRunRequest Valid() =>
        new(StartTime: ValidStart,
            SignupCloseTime: null,
            Description: null,
            Visibility: "PUBLIC",
            InstanceId: 1200,
            InstanceName: "Liberation of Undermine",
            Difficulty: "HEROIC",
            Size: 25,
            KeystoneLevel: null);

    [Fact]
    public void Accepts_structured_Difficulty_Size()
    {
        var result = Sut.Validate(Valid());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Rejects_missing_Difficulty()
    {
        var req = Valid() with { Difficulty = null };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("difficulty is required"));
    }

    [Fact]
    public void Rejects_missing_Size()
    {
        var req = Valid() with { Size = null };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("size is required"));
    }

    [Fact]
    public void Rejects_unknown_difficulty()
    {
        var req = Valid() with { Difficulty = "BRUTAL" };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("difficulty must be one of"));
    }

    [Fact]
    public void Rejects_size_out_of_range()
    {
        var result = Sut.Validate(Valid() with { Size = 50 });
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("size must be between 1 and 40"));
    }

    [Fact]
    public void Rejects_missing_InstanceId_for_non_MythicPlus()
    {
        var req = Valid() with { InstanceId = null };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("instanceId is required for non-Mythic+"));
    }

    [Fact]
    public void Accepts_MythicPlus_with_specific_instance()
    {
        var req = Valid() with
        {
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
            InstanceId = 500,
            InstanceName = "Ara-Kara, City of Echoes",
            KeystoneLevel = 15,
        };
        var result = Sut.Validate(req);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Accepts_MythicPlus_any_dungeon_when_keystone_level_is_specified()
    {
        var req = Valid() with
        {
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
            InstanceId = null,
            InstanceName = null,
            KeystoneLevel = 10,
        };
        var result = Sut.Validate(req);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Rejects_MythicPlus_when_both_instance_and_keystone_level_are_missing()
    {
        // "Any dungeon, any level" is nonsense — one of them must be specific.
        var req = Valid() with
        {
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
            InstanceId = null,
            InstanceName = null,
            KeystoneLevel = null,
        };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("keystoneLevel is required when no specific dungeon is selected"));
    }

    [Fact]
    public void Rejects_KeystoneLevel_on_non_MythicPlus_run()
    {
        var req = Valid() with { KeystoneLevel = 15 };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("keystoneLevel is only valid for Mythic+"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Rejects_KeystoneLevel_out_of_range(int level)
    {
        var req = Valid() with
        {
            Difficulty = "MYTHIC_KEYSTONE",
            Size = 5,
            InstanceId = null,
            InstanceName = null,
            KeystoneLevel = level,
        };
        var result = Sut.Validate(req);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("keystoneLevel must be between 2 and 30"));
    }
}
