// api/Services/EnrichmentPlanner.cs
// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Contracts.WoW;

namespace Lfm.Api.Services;

public sealed record EnrichmentPlan(bool FetchProfile, bool FetchSpecs, bool FetchMedia)
{
    public bool AnythingToFetch => FetchProfile || FetchSpecs || FetchMedia;
}

public static class EnrichmentPlanner
{
    private static readonly TimeSpan SpecsTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MediaTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan ProfileLowTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan ProfileMaxTtl = TimeSpan.FromHours(4);

    public static EnrichmentPlan Plan(StoredSelectedCharacter? existing, DateTimeOffset now)
    {
        if (existing is null)
            return new EnrichmentPlan(true, true, true);

        var profileTtl = (existing.Level ?? 0) >= WowConstants.MaxLevel
            ? ProfileMaxTtl : ProfileLowTtl;

        return new EnrichmentPlan(
            FetchProfile: IsExpired(existing.ProfileFetchedAt ?? existing.FetchedAt, profileTtl, now),
            FetchSpecs: IsExpired(existing.SpecsFetchedAt ?? existing.FetchedAt, SpecsTtl, now),
            FetchMedia: IsExpired(existing.MediaFetchedAt ?? existing.FetchedAt, MediaTtl, now));
    }

    private static bool IsExpired(string? isoTimestamp, TimeSpan ttl, DateTimeOffset now)
    {
        if (isoTimestamp is null) return true;
        if (!DateTimeOffset.TryParse(isoTimestamp, out var parsed)) return true;
        return now - parsed >= ttl;
    }
}
