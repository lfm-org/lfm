// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Privacy;

/// <summary>
/// Response body for <c>GET /api/privacy-contact/email</c>.
///
/// <para>
/// The SPA reveals this address behind a click-to-reveal button. To push the
/// scrape cost higher, the address is split into <c>local</c> and <c>domain</c>
/// parts; a naive HTML-scraping bot that captures the response verbatim does
/// not get a pre-assembled address. The <see cref="Email"/> field is kept for
/// one release as a transitional convenience for any SPA build that predates
/// the split-field consumer; new callers should always reassemble from
/// <see cref="Local"/> + "@" + <see cref="Domain"/>.
/// </para>
/// </summary>
public sealed record PrivacyEmailResponse(
    string Local,
    string Domain,
    [property: System.Obsolete("Read Local + '@' + Domain instead; the pre-assembled Email field is a one-release transitional convenience.")]
    string Email);
