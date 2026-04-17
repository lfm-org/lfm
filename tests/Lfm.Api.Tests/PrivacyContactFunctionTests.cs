// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Lfm.Api.Functions;
using Lfm.Contracts.Privacy;
using Xunit;

namespace Lfm.Api.Tests;

public class PrivacyContactFunctionTests
{
    private static readonly IConfiguration EmptyConfig =
        new ConfigurationBuilder().Build();

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        httpContext.Request.ContentType = "application/json";
        return httpContext.Request;
    }

    [Fact]
    public async Task Returns_ok_when_contact_request_is_valid()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.NotNull(ok.Value);

        // Verify the request was logged (without PII — only type)
        Assert.Single(
            log.Entries,
            e => e.Properties.ContainsKey("Type") && Equals(e.Properties["Type"], "data_request"));
    }

    [Fact]
    public async Task Returns_bad_request_when_email_is_missing()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badReq.StatusCode);
    }

    [Fact]
    public async Task Returns_bad_request_when_email_is_invalid()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "not-an-email",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badReq.StatusCode);
    }

    [Fact]
    public async Task Returns_bad_request_when_name_is_missing()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            email = "john@example.com",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badReq.StatusCode);
    }

    [Fact]
    public async Task Returns_bad_request_when_message_is_missing()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badReq.StatusCode);
    }

    [Fact]
    public async Task Returns_bad_request_when_type_is_missing()
    {
        var log = new TestLogger<PrivacyContactFunction>();
        var fn = new PrivacyContactFunction(log, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            message = "I want my data"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badReq.StatusCode);
    }
}
