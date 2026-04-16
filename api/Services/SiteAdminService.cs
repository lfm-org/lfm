// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Reads the site-admin allowlist from a secret store, with an in-memory cache
/// that expires every 60 seconds — matching the TypeScript CACHE_TTL_MS behaviour.
/// When KeyVaultUrl is not configured, IsAdminAsync always returns false.
/// </summary>
public sealed class SiteAdminService(IOptions<AuthOptions> authOpts, ISecretResolver secretResolver) : ISiteAdminService
{
    private const string SecretName = "site-admin-battle-net-ids";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AuthOptions _auth = authOpts.Value;
    private readonly ISecretResolver _secretResolver = secretResolver;

    // Simple lock-free cache: last loaded set + expiry.
    private volatile CacheEntry? _cache;

    public async Task<bool> IsAdminAsync(string battleNetId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(battleNetId)) return false;

        var ids = await GetAdminIdsAsync(ct);
        return ids.Contains(battleNetId);
    }

    private async Task<HashSet<string>> GetAdminIdsAsync(CancellationToken ct)
    {
        var cached = _cache;
        if (cached is not null && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Ids;

        var url = _auth.KeyVaultUrl?.Trim();
        if (string.IsNullOrEmpty(url))
        {
            _cache = new CacheEntry([], DateTimeOffset.UtcNow.Add(CacheTtl));
            return _cache.Ids;
        }

        try
        {
            var raw = await _secretResolver.GetSecretAsync(url, SecretName, ct);
            var ids = ParseAdminIds(raw);
            _cache = new CacheEntry(ids, DateTimeOffset.UtcNow.Add(CacheTtl));
            return ids;
        }
        catch
        {
            // On failure, extend the existing cache TTL to avoid hammering Key Vault.
            // If there is no prior cache, fall through with an empty set.
            if (cached is not null)
            {
                _cache = cached with { ExpiresAt = DateTimeOffset.UtcNow.Add(CacheTtl) };
                return cached.Ids;
            }
            return [];
        }
    }

    private static HashSet<string> ParseAdminIds(string? raw)
        => [.. (raw ?? string.Empty)
              .Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private sealed record CacheEntry(HashSet<string> Ids, DateTimeOffset ExpiresAt);
}
