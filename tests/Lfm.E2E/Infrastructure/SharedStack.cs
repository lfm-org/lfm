// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.E2E.Seeds;

namespace Lfm.E2E.Infrastructure;

/// <summary>
/// Static singleton that guarantees exactly one <see cref="StackFixture"/>
/// initialization per test run, regardless of how many collections exist.
/// Each collection fixture calls <see cref="GetAsync"/> to share the stack,
/// then creates its own IBrowserContext for isolation.
/// Seed data is run once during initialization — fixtures do not need to seed.
/// Cleanup is handled via <see cref="AppDomain.CurrentDomain.ProcessExit"/>.
/// </summary>
public static class SharedStack
{
    private static readonly Lazy<Task<StackFixture>> Instance = new(
        async () =>
        {
            var stack = new StackFixture();
            await stack.InitializeAsync();
            await DefaultSeed.SeedAsync(stack.CosmosClient, StackFixture.DatabaseName);
            return stack;
        },
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static bool _cleanupRegistered;
    private static readonly object CleanupLock = new();

    /// <summary>
    /// Returns the shared <see cref="StackFixture"/>, initializing it on the
    /// first call. Thread-safe and idempotent.
    /// </summary>
    public static Task<StackFixture> GetAsync()
    {
        EnsureCleanupRegistered();
        return Instance.Value;
    }

    private static void EnsureCleanupRegistered()
    {
        if (_cleanupRegistered) return;
        lock (CleanupLock)
        {
            if (_cleanupRegistered) return;
            AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            {
                if (!Instance.IsValueCreated) return;
                try
                {
                    Instance.Value.GetAwaiter().GetResult().DisposeAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Best effort — do not mask the primary test failure.
                }
            };
            _cleanupRegistered = true;
        }
    }
}
