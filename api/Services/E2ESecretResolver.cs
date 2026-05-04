// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

#if E2E
namespace Lfm.Api.Services;

/// <summary>
/// E2E-only secret resolver used by the containerized test API. It keeps the
/// site-admin journey local-first while exercising SiteAdminService's allowlist
/// path instead of bypassing it in the browser test.
/// </summary>
public sealed class E2ESecretResolver : ISecretResolver
{
    public Task<string?> GetSecretAsync(string vaultUrl, string secretName, CancellationToken ct)
        => Task.FromResult<string?>(
            string.Equals(secretName, "site-admin-battle-net-ids", StringComparison.Ordinal)
                ? "test-bnet-id-admin"
                : null);
}
#endif
