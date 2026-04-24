// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Lfm.Api.Functions;
using Xunit;

namespace Lfm.Api.Tests;

public class PrivacyContactFunctionTests
{
    private static IConfiguration Config(string? email = null)
    {
        var data = new Dictionary<string, string?>();
        if (email is not null)
        {
            data["PRIVACY_EMAIL"] = email;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void GetEmail_returns_200_with_email_when_configured()
    {
        var fn = new PrivacyContactFunction(Config("privacy@example.com"));

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);

        // Reflection access into the anonymous-type response body. The "email"
        // property name here matches the wire contract — response shape is
        // { "email": "..." } per PrivacyContactFunction.GetEmail. If the
        // property ever gets renamed or wrapped in a DTO, update here.
        var emailProp = ok.Value!.GetType().GetProperty("email")!.GetValue(ok.Value);
        Assert.Equal("privacy@example.com", emailProp);
    }

    [Fact]
    public void GetEmail_returns_404_when_not_configured()
    {
        var fn = new PrivacyContactFunction(Config(email: null));

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#privacy-email-unconfigured", problem.Type);
    }

    [Fact]
    public void GetEmail_returns_404_when_configured_value_is_empty()
    {
        var fn = new PrivacyContactFunction(Config(string.Empty));

        var result = fn.GetEmail(new DefaultHttpContext().Request);

        var notFound = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, notFound.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal("https://github.com/lfm-org/lfm/errors#privacy-email-unconfigured", problem.Type);
    }
}
