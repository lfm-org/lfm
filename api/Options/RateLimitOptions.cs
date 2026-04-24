// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int AuthRequestsPerMinute { get; set; } = 10;
    public int WriteRequestsPerMinute { get; set; } = 30;
    public int ReadRequestsPerMinute { get; set; } = 120;

    /// <summary>
    /// Tight ceiling on the privacy-contact email endpoint. The SPA reveals
    /// the address behind an explicit click, so a single browser session
    /// legitimately only calls this a handful of times per minute. Lowering
    /// the tier makes drive-by scraping more expensive without affecting
    /// real users.
    /// </summary>
    public int PrivacyRequestsPerMinute { get; set; } = 5;

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// IPs that may legitimately set X-Forwarded-For on the way in. If the
    /// request's TCP peer address appears here, the middleware trusts XFF and
    /// uses the rightmost entry (the last proxy-recorded client hop) as the
    /// rate-limit bucket key. Otherwise XFF is ignored and the bucket key is
    /// the TCP peer itself — a direct attacker cannot forge a new bucket.
    /// Configure via RateLimit:TrustedProxyAddresses:0, ...:1, ... — typically
    /// the Azure Front Door / SWA egress IPs. Empty by default.
    /// </summary>
    public IReadOnlyList<string> TrustedProxyAddresses { get; set; } = [];
}
