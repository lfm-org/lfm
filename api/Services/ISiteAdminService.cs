// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Services;

/// <summary>
/// Resolves whether a given battleNetId is a site administrator.
/// The list is read from a Key Vault secret named "site-admin-battle-net-ids",
/// mirroring the TypeScript implementation at functions/src/lib/site-admin-config.ts.
/// </summary>
public interface ISiteAdminService
{
    /// <summary>Returns true if battleNetId appears in the site-admin allowlist.</summary>
    Task<bool> IsAdminAsync(string battleNetId, CancellationToken ct);
}
