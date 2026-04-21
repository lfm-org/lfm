// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Functions;

/// <summary>
/// Timer-triggered weekly refresh of the static Blizzard reference blobs — see
/// <c>docs/storage-architecture.md</c>. Runs <see cref="IReferenceSync.SyncAllAsync"/>
/// to fetch the latest <c>journal-instance</c> + <c>playable-specialization</c>
/// indices from Blizzard, upload per-id detail + media blobs, and emit the
/// list-endpoint manifests at <c>reference/{kind}/index.json</c>.
///
/// Schedule: Sunday 04:00 UTC. Chosen to co-exist with <see cref="RaiderCleanupFunction"/>
/// (daily 04:00 UTC) without clashing on any one minute — Functions handles
/// multiple timer triggers at the same second fine, but staggering is cheap
/// insurance against observability noise. Blizzard reference data changes at
/// patch cadence (weeks to months); weekly is more than enough.
///
/// The same <see cref="IReferenceSync"/> code path serves both this timer and
/// the admin-only <see cref="WowReferenceRefreshFunction"/> HTTP endpoint, so
/// behaviour is identical between scheduled and ad-hoc invocations.
///
/// Ad-hoc invocation for a backfill (e.g. the first run after this ships) is
/// via the Functions host admin endpoint — timer triggers bypass the HTTP
/// <c>AuthPolicyMiddleware</c> entirely, so a Function App master key is
/// sufficient:
/// <code>
/// MASTER=$(az functionapp keys list -g lfm -n lfm-functions --query masterKey -o tsv)
/// curl -X POST -H "x-functions-key: $MASTER" \
///     https://lfm-functions.azurewebsites.net/admin/functions/wow-reference-refresh-timer \
///     -H "Content-Type: application/json" -d '{}'
/// </code>
/// </summary>
public class WowReferenceRefreshTimerFunction(
    IReferenceSync referenceSync,
    ILogger<WowReferenceRefreshTimerFunction> logger)
{
    [Function("wow-reference-refresh-timer")]
    public async Task Run(
        [TimerTrigger("0 0 4 * * SUN")] TimerInfo timer,
        CancellationToken ct)
    {
        logger.LogInformation("Starting weekly WoW reference sync");
        var response = await referenceSync.SyncAllAsync(ct);
        foreach (var result in response.Results)
        {
            logger.LogInformation(
                "WoW reference sync — {Entity}: {Status}",
                result.Name, result.Status);
        }
    }
}
