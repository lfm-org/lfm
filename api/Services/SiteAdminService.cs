using Azure.Security.KeyVault.Secrets;
using Azure.Identity;
using Microsoft.Extensions.Options;
using Lfm.Api.Options;

namespace Lfm.Api.Services;

/// <summary>
/// Reads the site-admin allowlist from Azure Key Vault, with an in-memory cache
/// that expires every 60 seconds — matching the TypeScript CACHE_TTL_MS behaviour.
/// When KeyVaultUrl is not configured, IsAdminAsync always returns false.
/// </summary>
public sealed class SiteAdminService(IOptions<AuthOptions> authOpts) : ISiteAdminService
{
    private const string SecretName = "site-admin-battle-net-ids";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly AuthOptions _auth = authOpts.Value;

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
            var client = new SecretClient(new Uri(url), new DefaultAzureCredential());
            var secret = await client.GetSecretAsync(SecretName, cancellationToken: ct);
            var ids = ParseAdminIds(secret.Value.Value);
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
