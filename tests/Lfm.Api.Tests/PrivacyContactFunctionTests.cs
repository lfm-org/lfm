// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Functions;
using Lfm.Api.Options;
using Lfm.Contracts.Privacy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using MSOptions = Microsoft.Extensions.Options.Options;

namespace Lfm.Api.Tests;

public class PrivacyContactFunctionTests
{
    private static PrivacyContactFunction MakeFunction(string? email = null)
        => new(MSOptions.Create(new PrivacyContactOptions { Email = email ?? string.Empty }));

    [Fact]
    public void GetEmail_returns_200_with_split_fields_when_configured()
    {
        var fn = MakeFunction("privacy@example.com");

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var body = Assert.IsType<PrivacyEmailResponse>(ok.Value);
        Assert.Equal("privacy", body.Local);
        Assert.Equal("example.com", body.Domain);
#pragma warning disable CS0618 // Transitional field assertion — removed in a later release.
        Assert.Equal("privacy@example.com", body.Email);
#pragma warning restore CS0618
    }

    [Fact]
    public void GetEmail_returns_404_when_not_configured()
    {
        var fn = MakeFunction(email: null);

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#privacy-email-unconfigured", problem.Type);
    }

    [Fact]
    public void GetEmail_returns_404_when_configured_value_is_empty()
    {
        var fn = MakeFunction(string.Empty);

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#privacy-email-unconfigured", problem.Type);
    }

    [Fact]
    public void GetEmail_returns_404_when_address_is_malformed()
    {
        // The Options validator normally rejects at startup, but a defensive
        // branch in the handler covers the case where validation is skipped
        // (tests, local settings) so a half-parsed address never leaks.
        var fn = MakeFunction("no-at-sign");

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
    }
}
