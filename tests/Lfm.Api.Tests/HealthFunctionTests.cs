using FluentAssertions;
using Lfm.Api.Functions;
using Lfm.Api.Options;
using Lfm.Contracts.Health;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using MsOptions = Microsoft.Extensions.Options.Options;
using Moq;
using Xunit;

namespace Lfm.Api.Tests;

public class HealthFunctionTests
{
    [Fact]
    public void Health_returns_ok_status_and_timestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var result = HealthFunction.Build();
        var after = DateTimeOffset.UtcNow;

        result.Should().BeOfType<HealthResponse>();
        result.Status.Should().Be("ok");
        result.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    private static (HealthFunction fn, Mock<Database> mockDb) CreateReadyFunction()
    {
        var mockDb = new Mock<Database>();
        var mockClient = new Mock<CosmosClient>();
        mockClient.Setup(c => c.GetDatabase("test-db")).Returns(mockDb.Object);

        var opts = MsOptions.Create(new CosmosOptions
        {
            Endpoint = "https://test.documents.azure.com",
            DatabaseName = "test-db",
        });

        return (new HealthFunction(mockClient.Object, opts), mockDb);
    }

    [Fact]
    public async Task Ready_returns_ok_with_ready_status_when_cosmos_is_reachable()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DatabaseResponse)null!);

        var before = DateTimeOffset.UtcNow;
        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        var body = ok.Value.Should().BeOfType<HealthResponse>().Subject;
        body.Status.Should().Be("ready");
        body.Timestamp.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);

        mockDb.Verify(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ready_returns_503_with_unready_when_cosmos_throws_cosmos_exception()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("Service unavailable", System.Net.HttpStatusCode.ServiceUnavailable, 0, "", 0));

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(503);
        obj.Value.Should().BeEquivalentTo(new { status = "unready", error = nameof(CosmosException) });
    }

    [Fact]
    public async Task Ready_returns_503_with_exception_type_name_for_unexpected_errors()
    {
        var (fn, mockDb) = CreateReadyFunction();
        mockDb.Setup(d => d.ReadAsync(It.IsAny<RequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connection pool exhausted"));

        var result = await fn.Ready(new DefaultHttpContext().Request, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(503);
        obj.Value.Should().BeEquivalentTo(new { status = "unready", error = nameof(InvalidOperationException) });
    }
}
