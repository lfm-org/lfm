// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Lfm.Api.Repositories;

/// <summary>
/// Structured-logging helpers for Cosmos <c>RequestCharge</c>. Emitting the
/// RU cost of every point-read, write, and query page into Application
/// Insights gives an at-a-glance view of which endpoints are expensive and
/// which partitions are hot — essential visibility on the free-tier 1000
/// RU/s ceiling. Log structure is deliberately minimal (op, container, pk,
/// ru) so the fields are indexable and queryable without parsing message
/// text.
/// </summary>
internal static class CosmosRequestChargeLogger
{
    public static void LogRequestCharge<T>(this ILogger logger, ItemResponse<T> response, string op, string container, string partitionKey)
    {
        logger.LogInformation(
            "Cosmos op={CosmosOp} container={CosmosContainer} pk={CosmosPartitionKey} ru={CosmosRequestCharge}",
            op, container, HashPartitionKey(partitionKey), response.RequestCharge);
    }

    public static void LogRequestCharge<T>(this ILogger logger, FeedResponse<T> response, string op, string container, string partitionKey)
    {
        logger.LogInformation(
            "Cosmos op={CosmosOp} container={CosmosContainer} pk={CosmosPartitionKey} ru={CosmosRequestCharge}",
            op, container, HashPartitionKey(partitionKey), response.RequestCharge);
    }

    /// <summary>
    /// Logs an accumulated <paramref name="requestCharge"/> for a multi-page
    /// cross-partition query. Callers sum the per-page charges themselves
    /// (one call per loop) and pass the total here so App Insights gets a
    /// single row per logical query rather than one per page.
    /// </summary>
    public static void LogRequestChargeTotal(this ILogger logger, string op, string container, double requestCharge)
    {
        logger.LogInformation(
            "Cosmos op={CosmosOp} container={CosmosContainer} pk={CosmosPartitionKey} ru={CosmosRequestCharge}",
            op, container, "*cross-partition*", requestCharge);
    }

    private static string HashPartitionKey(string partitionKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey));
        return Convert.ToHexString(bytes)[..16];
    }
}
