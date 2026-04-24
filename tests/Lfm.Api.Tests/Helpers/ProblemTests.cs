// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Diagnostics;
using Lfm.Api.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Lfm.Api.Tests.Helpers;

public class ProblemTests
{
    private static HttpContext NewContext(string path = "/api/runs/abc")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        return ctx;
    }

    private static ProblemDetails Payload(IActionResult result)
    {
        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Contains(Problem.ContentType, obj.ContentTypes);
        return Assert.IsType<ProblemDetails>(obj.Value);
    }

    [Fact]
    public void NotFound_builds_404_problem_with_type_uri_and_instance()
    {
        var ctx = NewContext("/api/runs/missing");

        var result = Problem.NotFound(ctx, "run-not-found", "Run does not exist.");

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, obj.StatusCode);
        Assert.Contains(Problem.ContentType, obj.ContentTypes);
        var body = Assert.IsType<ProblemDetails>(obj.Value);
        Assert.Equal($"{Problem.TypeBase}#run-not-found", body.Type);
        Assert.Equal("Not Found", body.Title);
        Assert.Equal(404, body.Status);
        Assert.Equal("Run does not exist.", body.Detail);
        Assert.Equal("/api/runs/missing", body.Instance);
    }

    [Fact]
    public void BadRequest_carries_supplied_extensions()
    {
        var ctx = NewContext();
        var errors = new Dictionary<string, object?>
        {
            ["errors"] = new Dictionary<string, string[]>
            {
                ["title"] = new[] { "Title is required." },
            },
        };

        var result = Problem.BadRequest(ctx, "validation-failed", "Request body failed validation.", errors);

        var body = Payload(result);
        Assert.Equal(400, body.Status);
        Assert.Equal($"{Problem.TypeBase}#validation-failed", body.Type);
        Assert.True(body.Extensions.ContainsKey("errors"));
    }

    [Fact]
    public void Unauthorized_builds_401_problem()
    {
        var result = Problem.Unauthorized(NewContext(), "auth-required");
        Assert.Equal(401, Payload(result).Status);
    }

    [Fact]
    public void Forbidden_builds_403_problem()
    {
        var result = Problem.Forbidden(NewContext(), "admin-only");
        Assert.Equal(403, Payload(result).Status);
    }

    [Fact]
    public void Conflict_builds_409_problem()
    {
        var result = Problem.Conflict(NewContext(), "signup-closed");
        Assert.Equal(409, Payload(result).Status);
    }

    [Fact]
    public void PreconditionFailed_builds_412_problem()
    {
        var result = Problem.PreconditionFailed(NewContext(), "etag-mismatch");
        Assert.Equal(412, Payload(result).Status);
    }

    [Fact]
    public void PayloadTooLarge_builds_413_problem()
    {
        var result = Problem.PayloadTooLarge(NewContext(), "body-too-large");
        Assert.Equal(413, Payload(result).Status);
    }

    [Fact]
    public void TooManyRequests_sets_Retry_After_when_seconds_supplied()
    {
        var ctx = NewContext();

        var result = Problem.TooManyRequests(ctx, "upstream-rate-limited", retryAfterSeconds: 45);

        Assert.Equal(429, Payload(result).Status);
        Assert.Equal("45", ctx.Response.Headers["Retry-After"].ToString());
    }

    [Fact]
    public void TooManyRequests_omits_Retry_After_when_seconds_missing()
    {
        var ctx = NewContext();

        Problem.TooManyRequests(ctx, "upstream-rate-limited");

        Assert.False(ctx.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public void UpstreamFailed_builds_502_problem()
    {
        var result = Problem.UpstreamFailed(NewContext(), "blizzard-unreachable");
        Assert.Equal(502, Payload(result).Status);
    }

    [Fact]
    public void InternalError_builds_500_problem()
    {
        var result = Problem.InternalError(NewContext(), "unexpected-error");
        Assert.Equal(500, Payload(result).Status);
    }

    [Fact]
    public void Problem_injects_traceId_when_Activity_is_current()
    {
        // Activity.Current is thread-local and relies on a live Activity — the
        // cheapest reliable way to stage one in-process is new Activity().Start().
        // ActivitySource.StartActivity would require an ActivityListener which
        // we don't otherwise need in this test.
        using var activity = new Activity("problem-traceid-test");
        activity.Start();

        var result = Problem.NotFound(NewContext(), "example");

        var body = Payload(result);
        Assert.True(body.Extensions.ContainsKey("traceId"));
        var traceId = Assert.IsType<string>(body.Extensions["traceId"]);
        Assert.False(string.IsNullOrEmpty(traceId));
    }

    [Fact]
    public void Problem_has_no_traceId_when_no_Activity_is_current()
    {
        Activity.Current = null;

        var result = Problem.NotFound(NewContext(), "example");

        var body = Payload(result);
        Assert.False(body.Extensions.ContainsKey("traceId"));
    }
}
