// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Lfm.Api.Services;
using Moq;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class BlizzardRateLimitHandlerTests
{
    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage _, CancellationToken __)
        {
            Calls++;
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task Send_returns_503_when_limiter_denies_acquisition()
    {
        var limiter = new Mock<IBlizzardRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BlizzardLease(IsAcquired: false));

        var inner = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new BlizzardRateLimitHandler(limiter.Object) { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://eu.api.blizzard.com/foo");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Send_forwards_to_inner_when_lease_acquired()
    {
        var limiter = new Mock<IBlizzardRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BlizzardLease(IsAcquired: true));

        var inner = new StubHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new BlizzardRateLimitHandler(limiter.Object) { InnerHandler = inner };
        var client = new HttpClient(handler);

        var response = await client.GetAsync("https://eu.api.blizzard.com/foo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Send_pauses_limiter_on_upstream_429_with_Retry_After()
    {
        var limiter = new Mock<IBlizzardRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BlizzardLease(IsAcquired: true));

        var upstream429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        upstream429.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

        var inner = new StubHandler(upstream429);
        var handler = new BlizzardRateLimitHandler(limiter.Object) { InnerHandler = inner };
        var client = new HttpClient(handler);

        await client.GetAsync("https://eu.api.blizzard.com/foo");

        limiter.Verify(l => l.PauseUntil(It.Is<DateTimeOffset>(d => d > DateTimeOffset.UtcNow)), Times.Once);
    }
}
