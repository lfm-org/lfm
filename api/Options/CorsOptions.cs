// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";
    public required string[] AllowedOrigins { get; init; }
}
