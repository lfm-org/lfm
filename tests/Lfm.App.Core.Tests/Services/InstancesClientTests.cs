using System.Net;
using FluentAssertions;
using Lfm.App.Services;
using Lfm.Contracts.Instances;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class InstancesClientTests
{
    private static (InstancesClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new InstancesClient(factory.Object), handler);
    }

    [Fact]
    public async Task ListAsync_returns_instances_on_success()
    {
        var instances = new[]
        {
            new InstanceDto("liberation-of-undermine", "Liberation of Undermine", "raid", "tww"),
            new InstanceDto("operation-mechagon", "Operation: Mechagon", "mythic-plus", "bfa"),
        };
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, instances));

        var result = await client.ListAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("liberation-of-undermine");
        result[1].Name.Should().Be("Operation: Mechagon");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/instances");
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_body_is_null()
    {
        // Server returns 200 with empty body — InstancesClient must coalesce to empty list,
        // not propagate null. Pinning the null-coalescing default at the public boundary.
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
        });
        var (client, _) = MakeClient(handler);

        var result = await client.ListAsync(CancellationToken.None);

        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_body_is_empty_array()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, Array.Empty<InstanceDto>()));

        var result = await client.ListAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListAsync_throws_HttpRequestException_on_5xx()
    {
        // InstancesClient doesn't catch — pinning current contract.
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        var act = () => client.ListAsync(CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
