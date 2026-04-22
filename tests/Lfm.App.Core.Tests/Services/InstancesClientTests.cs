// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
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
            new InstanceDto("1234:raid", 1234, "Liberation of Undermine", "raid", "tww"),
            new InstanceDto("5678:mythic-plus", 5678, "Operation: Mechagon", "mythic-plus", "bfa"),
        };
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, instances));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("1234:raid", result[0].Id);
        Assert.Equal("Operation: Mechagon", result[1].Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/wow/reference/instances", handler.LastRequest.RequestUri!.PathAndQuery);
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

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_body_is_empty_array()
    {
        var (client, _) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, Array.Empty<InstanceDto>()));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ListAsync_throws_HttpRequestException_on_5xx()
    {
        // InstancesClient doesn't catch — pinning current contract.
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListAsync(CancellationToken.None));
    }
}
