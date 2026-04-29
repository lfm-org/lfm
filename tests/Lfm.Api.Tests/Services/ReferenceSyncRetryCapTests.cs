// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class ReferenceSyncRetryCapTests
{
    // Cap semantics: maxRetries=N produces N+1 total fetch attempts (1 initial + N retries)
    // before the cap fires. The boundary cases pin off-by-one behavior at N=0 and N=1.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    [InlineData(5, 6)]
    public async Task FetchWithRetryAsync_returns_null_after_cap_on_sustained_429(
        int maxRetries, int expectedCalls)
    {
        var calls = 0;
        Task<string> Fetch()
        {
            calls++;
            throw new HttpRequestException(
                "Too Many Requests", inner: null, statusCode: HttpStatusCode.TooManyRequests);
        }

        var result = await Lfm.Api.Services.ReferenceSync.FetchWithRetryAsyncForTests(
            Fetch, "test-fetch", maxRetries: maxRetries, retryDelay: TimeSpan.Zero,
            logger: NullLogger.Instance, ct: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(expectedCalls, calls);
    }

    [Fact]
    public async Task FetchWithRetryAsync_returns_null_on_non_429_exception_without_retry()
    {
        var calls = 0;
        Task<string> Fetch()
        {
            calls++;
            throw new HttpRequestException(
                "Server Error", inner: null, statusCode: HttpStatusCode.InternalServerError);
        }

        var result = await Lfm.Api.Services.ReferenceSync.FetchWithRetryAsyncForTests(
            Fetch, "test-fetch", maxRetries: 5, retryDelay: TimeSpan.Zero,
            logger: NullLogger.Instance, ct: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, calls);
    }
}
