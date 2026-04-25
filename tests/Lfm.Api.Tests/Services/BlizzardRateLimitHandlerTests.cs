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

    [Fact]
    public async Task Send_clamps_oversized_Retry_After_to_MaxRetryAfter()
    {
        // Adversarial / buggy upstream: Retry-After far beyond any plausible window.
        // The handler must clamp to MaxRetryAfter so the shared limiter does not
        // pause every authenticated user's outbound traffic for hours/days.
        var limiter = new Mock<IBlizzardRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BlizzardLease(IsAcquired: true));

        var upstream429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        upstream429.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(999_999));

        var inner = new StubHandler(upstream429);
        var handler = new BlizzardRateLimitHandler(limiter.Object) { InnerHandler = inner };
        var client = new HttpClient(handler);

        var before = DateTimeOffset.UtcNow;
        await client.GetAsync("https://eu.api.blizzard.com/foo");

        // The PauseUntil argument must not exceed `before + MaxRetryAfter` (plus
        // a small clock-tolerance margin). 5 minutes is generous and still rejects
        // the unbounded case (~11 days) by a wide margin.
        limiter.Verify(l => l.PauseUntil(It.Is<DateTimeOffset>(
            d => d <= before + BlizzardRateLimitHandler.MaxRetryAfter + TimeSpan.FromMinutes(5))),
            Times.Once);
    }

    [Fact]
    public async Task Send_floors_negative_Retry_After_to_one_second()
    {
        // Retry-After Date in the past resolves to a negative TimeSpan; should be
        // floored to 1s rather than producing a PauseUntil <= now (which would
        // be a no-op or even rewind the limiter clock).
        var limiter = new Mock<IBlizzardRateLimiter>();
        limiter.Setup(l => l.AcquireAsync(It.IsAny<CancellationToken>()))
               .ReturnsAsync(new BlizzardLease(IsAcquired: true));

        var upstream429 = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        upstream429.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            DateTimeOffset.UtcNow - TimeSpan.FromMinutes(5));

        var inner = new StubHandler(upstream429);
        var handler = new BlizzardRateLimitHandler(limiter.Object) { InnerHandler = inner };
        var client = new HttpClient(handler);

        var before = DateTimeOffset.UtcNow;
        await client.GetAsync("https://eu.api.blizzard.com/foo");

        limiter.Verify(l => l.PauseUntil(It.Is<DateTimeOffset>(
            d => d > before)),
            Times.Once);
    }
}
