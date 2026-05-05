// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.App.Services;
using Lfm.Contracts.Specializations;
using Moq;
using Xunit;

namespace Lfm.App.Core.Tests.Services;

public class SpecializationsClientTests
{
    private static (SpecializationsClient client, StubHttpMessageHandler handler) MakeClient(
        StubHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:7071/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("api")).Returns(http);
        return (new SpecializationsClient(factory.Object), handler);
    }

    [Fact]
    public async Task ListAsync_returns_specializations_on_success()
    {
        var specializations = new[]
        {
            new SpecializationDto(257, "Holy", 5, "HEALER", "https://render.example/holy.jpg"),
            new SpecializationDto(258, "Shadow", 5, "DPS", "https://render.example/shadow.jpg"),
        };
        var (client, handler) = MakeClient(StubHttpMessageHandler.Json(HttpStatusCode.OK, specializations));

        var result = await client.ListAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(257, result[0].Id);
        Assert.Equal("https://render.example/shadow.jpg", result[1].IconUrl);
        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Equal("/api/v1/wow/reference/specializations", handler.LastRequest.RequestUri!.PathAndQuery);
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
