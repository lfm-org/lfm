using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
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
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().NotBeNull();

        // Verify the request was logged (without PII — only type)
        log.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("data_request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Returns_bad_request_when_email_is_missing()
    {
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Returns_bad_request_when_email_is_invalid()
    {
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "not-an-email",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Returns_bad_request_when_name_is_missing()
    {
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            email = "john@example.com",
            message = "I want my data",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Returns_bad_request_when_message_is_missing()
    {
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            type = "data_request"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Returns_bad_request_when_type_is_missing()
    {
        var log = new Mock<ILogger<PrivacyContactFunction>>();
        var fn = new PrivacyContactFunction(log.Object, EmptyConfig);

        var req = MakeRequest(new
        {
            name = "John Doe",
            email = "john@example.com",
            message = "I want my data"
        });

        var result = await fn.Run(req, CancellationToken.None);

        var badReq = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badReq.StatusCode.Should().Be(400);
    }
}
