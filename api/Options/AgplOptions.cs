// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Options;

/// <summary>
/// AGPL §13 requires operators of network-interactive modified versions to
/// prominently offer users the Corresponding Source. Fork operators configure
/// <see cref="SourceRepositoryUrl"/> to point at their own source repository;
/// the middleware layer advertises it as a response header on every request.
/// </summary>
public sealed class AgplOptions
{
    public const string SectionName = "Agpl";

    /// <summary>
    /// Public URL at which the Corresponding Source for this deployment is
    /// available. Defaults to the canonical upstream repository; fork operators
    /// MUST override this to point at their own modified source to satisfy
    /// AGPL §13.
    /// </summary>
    public string SourceRepositoryUrl { get; init; } = "https://github.com/lfm-org/lfm";
}
