// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.ComponentModel.DataAnnotations;

namespace Lfm.Api.Options;

/// <summary>
/// Caps the size of inbound request bodies. The Azure Functions platform
/// default is ~100 MB; the API never accepts payloads that large, so an
/// earlier-in-the-pipeline guard keeps runaway uploads from burning RU
/// or blob bandwidth. Fork operators can raise the cap by configuration
/// if they need to accept larger payloads (e.g. image uploads) but the
/// default is deliberately conservative.
/// </summary>
public sealed class RequestSizeLimitOptions
{
    public const string SectionName = "RequestSizeLimit";

    /// <summary>
    /// Maximum allowed <c>Content-Length</c> in bytes. Requests advertising
    /// a larger body are rejected with 413 Payload Too Large before the
    /// handler runs. Default is 64 KiB — larger than any legitimate write
    /// on the current surface (the biggest being a full guild rank-permission
    /// PATCH).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxBytes { get; init; } = 65_536;
}
