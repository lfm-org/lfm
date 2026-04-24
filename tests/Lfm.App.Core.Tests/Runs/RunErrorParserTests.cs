// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Runs;
using Xunit;

namespace Lfm.App.Core.Tests.Runs;

public class RunErrorParserTests
{
    [Fact]
    public void BadRequest_with_errors_array_classifies_as_Validation_with_each_message()
    {
        var body = """{"type":"https://github.com/lfm-org/lfm/errors#validation-failed","title":"Bad Request","status":400,"detail":"Request body failed validation.","errors":["startTime is required","modeKey must be at most 64 characters"]}""";
        var result = RunErrorParser.Parse(HttpStatusCode.BadRequest, body);
        Assert.Equal(RunErrorKind.Validation, result.Kind);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("startTime is required", result.Messages[0]);
    }

    [Fact]
    public void BadRequest_with_detail_only_classifies_as_Validation_with_that_message()
    {
        var body = """{"type":"https://github.com/lfm-org/lfm/errors#guild-required","title":"Bad Request","status":400,"detail":"A guild run requires an active character in a guild."}""";
        var result = RunErrorParser.Parse(HttpStatusCode.BadRequest, body);
        Assert.Equal(RunErrorKind.Validation, result.Kind);
        Assert.Equal("A guild run requires an active character in a guild.", Assert.Single(result.Messages));
    }

    [Fact]
    public void BadRequest_with_empty_body_classifies_as_Validation_with_no_messages()
    {
        var result = RunErrorParser.Parse(HttpStatusCode.BadRequest, "");
        Assert.Equal(RunErrorKind.Validation, result.Kind);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Forbidden_with_rank_denied_type_classifies_as_GuildRankDenied()
    {
        var body = """{"type":"https://github.com/lfm-org/lfm/errors#guild-rank-denied","title":"Forbidden","status":403,"detail":"Guild run creation is not enabled for your rank."}""";
        var result = RunErrorParser.Parse(HttpStatusCode.Forbidden, body);
        Assert.Equal(RunErrorKind.GuildRankDenied, result.Kind);
        Assert.True(result.IsGuildRankDenied);
        Assert.Contains("rank", Assert.Single(result.Messages), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Forbidden_with_detail_but_unknown_type_still_surfaces_server_detail()
    {
        var body = """{"type":"https://github.com/lfm-org/lfm/errors#some-other-thing","title":"Forbidden","status":403,"detail":"Specific reason."}""";
        var result = RunErrorParser.Parse(HttpStatusCode.Forbidden, body);
        Assert.Equal(RunErrorKind.GuildRankDenied, result.Kind);
        Assert.Equal("Specific reason.", Assert.Single(result.Messages));
    }

    [Fact]
    public void Forbidden_with_empty_body_still_classifies_as_GuildRankDenied()
    {
        var result = RunErrorParser.Parse(HttpStatusCode.Forbidden, null);
        Assert.Equal(RunErrorKind.GuildRankDenied, result.Kind);
        Assert.NotEmpty(result.Messages);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void Server_5xx_classifies_as_Network(HttpStatusCode status)
    {
        var result = RunErrorParser.Parse(status, "doesn't matter");
        Assert.Equal(RunErrorKind.Network, result.Kind);
        Assert.True(result.IsNetwork);
    }

    [Fact]
    public void Unhandled_status_classifies_as_Unknown()
    {
        var result = RunErrorParser.Parse(HttpStatusCode.NotFound, null);
        Assert.Equal(RunErrorKind.Unknown, result.Kind);
    }

    [Fact]
    public void Network_wraps_an_exception_message()
    {
        var result = RunErrorParser.Network(new HttpRequestException("connection refused"));
        Assert.Equal(RunErrorKind.Network, result.Kind);
        Assert.Equal("connection refused", Assert.Single(result.Messages));
    }

    [Fact]
    public void Malformed_JSON_body_falls_through_to_raw_text_on_400()
    {
        var body = "not json at all";
        var result = RunErrorParser.Parse(HttpStatusCode.BadRequest, body);
        Assert.Equal(RunErrorKind.Validation, result.Kind);
        Assert.Equal(body, Assert.Single(result.Messages));
    }
}
