// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int AuthRequestsPerMinute { get; set; } = 10;
    public int WriteRequestsPerMinute { get; set; } = 30;
    public bool Enabled { get; set; } = true;
}
