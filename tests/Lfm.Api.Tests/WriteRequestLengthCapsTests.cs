// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Validation;
using Lfm.Contracts.Guild;
using Lfm.Contracts.Runs;
using Xunit;

namespace Lfm.Api.Tests;

/// <summary>
/// Pins length caps on free-text fields in write DTOs. Keeps stored-document
/// sizes bounded — Cosmos bills per KB, and payload is also carried back to
/// the client on list endpoints. Each field has a separate test so a regression
/// points at the specific property that lost its cap.
/// </summary>
public class WriteRequestLengthCapsTests
{
    private static string LongerThan(int limit) => new string('x', limit + 1);

    // ------------------------------------------------------------------
    // CreateRunRequest
    // ------------------------------------------------------------------

    [Fact]
    public void CreateRun_description_over_2000_chars_fails()
    {
        var req = new CreateRunRequest(
            StartTime: "2026-05-01T19:00:00Z",
            SignupCloseTime: null,
            Description: LongerThan(2000),
            Visibility: "GUILD",
            InstanceId: 631,
            Difficulty: "NORMAL",
            Size: 10,
            InstanceName: null);

        var result = new CreateRunRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Description");
    }

    [Fact]
    public void CreateRun_description_at_2000_chars_passes()
    {
        var req = new CreateRunRequest(
            StartTime: "2026-05-01T19:00:00Z",
            SignupCloseTime: null,
            Description: new string('x', 2000),
            Visibility: "GUILD",
            InstanceId: 631,
            Difficulty: "NORMAL",
            Size: 10,
            InstanceName: null);

        var result = new CreateRunRequestValidator().Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateRun_instanceName_over_128_chars_fails()
    {
        var req = new CreateRunRequest(
            StartTime: "2026-05-01T19:00:00Z",
            SignupCloseTime: null,
            Description: null,
            Visibility: "GUILD",
            InstanceId: 631,
            Difficulty: "NORMAL",
            Size: 10,
            InstanceName: LongerThan(128));

        var result = new CreateRunRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "InstanceName");
    }

    // ------------------------------------------------------------------
    // UpdateRunRequest — all fields optional, caps only apply when provided
    // ------------------------------------------------------------------

    [Fact]
    public void UpdateRun_description_over_2000_chars_fails()
    {
        var req = new UpdateRunRequest(
            StartTime: null,
            SignupCloseTime: null,
            Description: LongerThan(2000),
            Visibility: null,
            InstanceId: null,
            InstanceName: null);

        var result = new UpdateRunRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Description");
    }

    [Fact]
    public void UpdateRun_all_null_passes()
    {
        // Sanity: length rules must not fire when the field is not supplied.
        var req = new UpdateRunRequest(
            StartTime: null,
            SignupCloseTime: null,
            Description: null,
            Visibility: null,
            InstanceId: null,
            InstanceName: null);

        var result = new UpdateRunRequestValidator().Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateRun_instanceName_over_128_chars_fails()
    {
        var req = new UpdateRunRequest(
            StartTime: null,
            SignupCloseTime: null,
            Description: null,
            Visibility: null,
            InstanceId: null,
            InstanceName: LongerThan(128));

        var result = new UpdateRunRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "InstanceName");
    }

    // ------------------------------------------------------------------
    // UpdateGuildRequest
    // ------------------------------------------------------------------

    [Fact]
    public void UpdateGuild_slogan_over_200_chars_fails()
    {
        var req = new UpdateGuildRequest(
            Timezone: "Europe/Helsinki",
            Locale: "en-gb",
            Slogan: LongerThan(200),
            RankPermissions: null);

        var result = new UpdateGuildRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Slogan");
    }

    [Fact]
    public void UpdateGuild_slogan_at_200_chars_passes()
    {
        var req = new UpdateGuildRequest(
            Timezone: "Europe/Helsinki",
            Locale: "en-gb",
            Slogan: new string('x', 200),
            RankPermissions: null);

        var result = new UpdateGuildRequestValidator().Validate(req);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateGuild_timezone_over_64_chars_fails()
    {
        var req = new UpdateGuildRequest(
            Timezone: LongerThan(64),
            Locale: "en-gb",
            Slogan: null,
            RankPermissions: null);

        var result = new UpdateGuildRequestValidator().Validate(req);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Timezone");
    }
}
