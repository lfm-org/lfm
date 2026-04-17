// tests/Lfm.Api.Tests/Services/EnrichmentPlannerTests.cs
// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;
using Lfm.Api.Services;
using Lfm.Contracts.WoW;
using Xunit;

namespace Lfm.Api.Tests.Services;

public class EnrichmentPlannerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-17T10:00:00Z");
    private static string Iso(DateTimeOffset d) => d.ToString("O");

    [Fact]
    public void Null_existing_fetches_all_tiers()
    {
        var plan = EnrichmentPlanner.Plan(null, Now);
        Assert.True(plan.FetchProfile);
        Assert.True(plan.FetchSpecs);
        Assert.True(plan.FetchMedia);
    }

    [Fact]
    public void All_fresh_returns_no_fetches()
    {
        var c = MakeChar(
            profile: Iso(Now.AddMinutes(-30)),
            specs: Iso(Now.AddMinutes(-10)),
            media: Iso(Now.AddHours(-12)),
            level: 30);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.False(plan.FetchProfile);
        Assert.False(plan.FetchSpecs);
        Assert.False(plan.FetchMedia);
    }

    [Fact]
    public void Specs_older_than_15min_triggers_specs_only()
    {
        var c = MakeChar(
            profile: Iso(Now.AddMinutes(-30)),
            specs: Iso(Now.AddMinutes(-20)),
            media: Iso(Now.AddHours(-12)),
            level: 30);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.False(plan.FetchProfile);
        Assert.True(plan.FetchSpecs);
        Assert.False(plan.FetchMedia);
    }

    [Fact]
    public void Profile_older_than_1h_at_low_level_triggers_profile()
    {
        var c = MakeChar(
            profile: Iso(Now.AddHours(-2)),
            specs: Iso(Now.AddMinutes(-5)),
            media: Iso(Now.AddHours(-12)),
            level: 50);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.True(plan.FetchProfile);
    }

    [Fact]
    public void Profile_older_than_1h_at_max_level_still_fresh_within_4h()
    {
        var c = MakeChar(
            profile: Iso(Now.AddHours(-2)),
            specs: Iso(Now.AddMinutes(-5)),
            media: Iso(Now.AddHours(-12)),
            level: WowConstants.MaxLevel);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.False(plan.FetchProfile);
    }

    [Fact]
    public void Profile_older_than_4h_at_max_level_triggers_profile()
    {
        var c = MakeChar(
            profile: Iso(Now.AddHours(-5)),
            specs: Iso(Now.AddMinutes(-5)),
            media: Iso(Now.AddHours(-12)),
            level: WowConstants.MaxLevel);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.True(plan.FetchProfile);
    }

    [Fact]
    public void Media_older_than_24h_triggers_media()
    {
        var c = MakeChar(
            profile: Iso(Now.AddMinutes(-30)),
            specs: Iso(Now.AddMinutes(-5)),
            media: Iso(Now.AddHours(-25)),
            level: 30);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.True(plan.FetchMedia);
    }

    [Fact]
    public void Legacy_FetchedAt_seeds_null_tier_timestamps_when_fresh()
    {
        var c = MakeChar(profile: null, specs: null, media: null, level: 30)
            with
        { FetchedAt = Iso(Now.AddMinutes(-5)) };
        var plan = EnrichmentPlanner.Plan(c, Now);
        // 5 min ago: profile (1h) fresh, specs (15 min) fresh, media (24h) fresh.
        Assert.False(plan.FetchProfile);
        Assert.False(plan.FetchSpecs);
        Assert.False(plan.FetchMedia);
    }

    [Fact]
    public void Unparseable_timestamp_treated_as_expired()
    {
        var c = MakeChar(profile: "not-a-date", specs: null, media: null, level: 30);
        var plan = EnrichmentPlanner.Plan(c, Now);
        Assert.True(plan.FetchProfile);
    }

    private static StoredSelectedCharacter MakeChar(string? profile, string? specs, string? media, int? level)
        => new(
            Id: "eu-stormreaver-shalena",
            Region: "eu",
            Realm: "stormreaver",
            Name: "Shalena",
            Level: level,
            ProfileFetchedAt: profile,
            SpecsFetchedAt: specs,
            MediaFetchedAt: media);
}
