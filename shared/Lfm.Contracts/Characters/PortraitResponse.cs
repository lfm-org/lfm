// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Characters;

/// <summary>
/// Response for the POST /api/battlenet/character-portraits endpoint.
/// Maps "{region}-{realm}-{name}" character IDs to their portrait URLs.
///
/// Mirrors the <c>Record&lt;string, string&gt;</c> shape returned by
/// <c>functions/src/functions/battlenet-character-portraits.ts</c>.
/// </summary>
public sealed record PortraitResponse(IReadOnlyDictionary<string, string> Portraits);
