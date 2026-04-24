// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Services;
using Lfm.Contracts.Expansions;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class ExpansionsClientTests
{
    private static (ExpansionsClient client, StubHttpMessageHandler handler) MakeClient(StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new ExpansionsClient(factory.Object), handler);
    }

    [Fact]
    public async Task ListAsync_returns_expansions_on_success()
    {
        var expansions = new[]
        {
            new ExpansionDto(68, "Classic"),
            new ExpansionDto(505, "The War Within"),
        };
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, expansions));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(68, result[0].Id);
        Assert.Equal("The War Within", result[1].Name);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/wow/reference/expansions", handler.LastRequest.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_body_is_null()
    {
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
    public async Task ListAsync_throws_HttpRequestException_on_5xx()
    {
        var (client, _) = MakeClient(new StubHttpMessageHandler(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListAsync(CancellationToken.None));
    }
}
