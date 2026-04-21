// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Runs;

/// <summary>
/// Paginated response for <c>GET /api/runs</c>. One page of visible runs plus
/// an opaque continuation token the client hands back to fetch the next page.
/// A null token means the server has no more results for this query. The
/// server caps <c>Items.Count</c> at the service-defined maximum (currently
/// 200); the client MUST NOT assume a full-page response implies no more data.
/// </summary>
public sealed record RunsListResponse(
    IReadOnlyList<RunSummaryDto> Items,
    string? ContinuationToken);
