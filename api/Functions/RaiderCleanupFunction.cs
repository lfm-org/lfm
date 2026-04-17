// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Lfm.Api.Audit;
using Lfm.Api.Repositories;

namespace Lfm.Api.Functions;

/// <summary>
/// Timer-triggered function that scrubs and deletes raider accounts that have
/// been inactive for more than 90 days (no login in the last 90 days).
///
/// Mirrors raider-cleanup.ts:
///   schedule: "0 0 4 * * *"  — fires daily at 04:00 UTC.
///   query:    SELECT id, battleNetId WHERE lastSeenAt &lt; cutoff OR NOT IS_DEFINED(lastSeenAt)
///   for each: scrubRaiderFromRuns → deleteRaiderDocument → audit log
/// </summary>
public class RaiderCleanupFunction(
    IRaidersRepository raidersRepo,
    IRunsRepository runsRepo,
    ILogger<RaiderCleanupFunction> logger)
{
    // 90 days in milliseconds — mirrors NINETY_DAYS_MS in raider-cleanup.ts.
    private static readonly TimeSpan NinetyDays = TimeSpan.FromDays(90);

    [Function("raider-cleanup")]
    public async Task Run(
        [TimerTrigger("0 0 4 * * *")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(NinetyDays).ToString("o");
        var staleRaiders = await raidersRepo.ListExpiredAsync(cutoff, cancellationToken);

        var removed = 0;
        var errors = 0;

        foreach (var raider in staleRaiders)
        {
            try
            {
                // Mirror TS order: scrub run references first, then delete the raider document.
                await runsRepo.ScrubRaiderAsync(raider.BattleNetId, cancellationToken);
                await raidersRepo.DeleteAsync(raider.BattleNetId, cancellationToken);
                AuditLog.Emit(logger, new AuditEvent("account.expired", "system", raider.BattleNetId, "success", null));
                removed++;
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(
                    ex,
                    "Raider cleanup: failed to remove account {BattleNetId}",
                    raider.BattleNetId);
            }
        }

        logger.LogInformation(
            "Raider cleanup: removed {Removed} inactive account(s){ErrorSuffix}",
            removed,
            errors > 0 ? $", {errors} error(s)" : string.Empty);
    }
}
