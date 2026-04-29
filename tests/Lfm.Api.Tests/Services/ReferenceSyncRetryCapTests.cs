// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class ReferenceSyncRetryCapTests
{
    [Fact]
    public async Task FetchWithRetryAsync_returns_null_after_cap_exceeded_on_sustained_429()
    {
        var calls = 0;
        Task<string> Fetch()
        {
            calls++;
            throw new HttpRequestException(
                "Too Many Requests", inner: null, statusCode: HttpStatusCode.TooManyRequests);
        }

        var result = await Lfm.Api.Services.ReferenceSync.FetchWithRetryAsyncForTests(
            Fetch, "test-fetch", maxRetries: 3, retryDelay: TimeSpan.Zero,
            logger: NullLogger.Instance, ct: CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(4, calls); // initial attempt + 3 retries
    }
}
