// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reflection;
using Xunit;

namespace Lfm.Api.Tests;

public class CosmosRequestChargeLoggerTests
{
    [Fact]
    public void LogRequestCharge_does_not_emit_raw_partition_key()
    {
        var logger = new TestLogger<CosmosRequestChargeLoggerTests>();
        var response = CreateItemResponse();
        const string rawPartitionKey = "run-42\r\nforged=true";

        logger.LogRequestCharge(response, "read", "runs", rawPartitionKey);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        var loggedPartitionKey = Assert.IsType<string>(entry.Properties["CosmosPartitionKey"]);
        Assert.DoesNotContain(rawPartitionKey, entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(rawPartitionKey, loggedPartitionKey, StringComparison.Ordinal);
        Assert.Matches("^[0-9A-F]{16}$", loggedPartitionKey);
    }

    private static ItemResponse<object> CreateItemResponse()
    {
        var headers = new Headers();
        headers.Set("x-ms-request-charge", "1.25");

        return (ItemResponse<object>)Activator.CreateInstance(
            typeof(ItemResponse<object>),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [HttpStatusCode.OK, headers, new object(), null, null],
            culture: null)!;
    }
}
