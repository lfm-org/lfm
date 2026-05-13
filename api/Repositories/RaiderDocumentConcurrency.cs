// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Repositories;

internal static class RaiderDocumentConcurrency
{
    private const int MaxAttempts = 3;

    public static async Task<RaiderDocument?> ReplaceWithRetryAsync(
        IRaidersRepository repo,
        RaiderDocument initial,
        Func<RaiderDocument, RaiderDocument> apply,
        CancellationToken ct)
    {
        var current = initial;
        for (var attempt = 1; ; attempt++)
        {
            var updated = apply(current);
            if (string.IsNullOrEmpty(current.ETag))
            {
                await repo.UpsertAsync(updated, ct);
                return updated;
            }

            try
            {
                return await repo.ReplaceAsync(updated, current.ETag, ct);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                var latest = await repo.GetByBattleNetIdAsync(current.BattleNetId, ct);
                if (latest is null)
                    return null;

                current = latest;
            }
        }
    }
}
