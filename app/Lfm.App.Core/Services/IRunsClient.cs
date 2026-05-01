// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Runs;

namespace Lfm.App.Services;

/// <summary>
/// A run-detail response paired with its HTTP <c>ETag</c>. Pages that plan
/// to PUT a subsequent update echo the ETag back as <c>If-Match</c> so the
/// server can enforce optimistic concurrency.
/// </summary>
public sealed record RunDetailWithEtag(RunDetailDto Run, string? ETag);

public interface IRunsClient
{
    Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct);
    Task<RunDetailDto?> GetAsync(string id, CancellationToken ct);

    /// <summary>
    /// GET /api/runs/{id} that also captures the response ETag so the
    /// caller can echo it on a subsequent PUT as <c>If-Match</c>.
    /// </summary>
    Task<RunDetailWithEtag?> GetWithEtagAsync(string id, CancellationToken ct);

    Task<RunDetailDto?> CreateAsync(CreateRunRequest request, CancellationToken ct);

    /// <summary>
    /// PUT /api/runs/{id} guarded by <paramref name="ifMatchEtag"/>. Throws
    /// <see cref="Runs.StaleEtagException"/> when the server rejects the
    /// request with 412 Precondition Failed so the caller can prompt the
    /// user to reload instead of showing a generic "save failed" error.
    /// </summary>
    Task<RunDetailWithEtag?> UpdateAsync(string id, UpdateRunRequest request, string ifMatchEtag, CancellationToken ct);

    Task<bool> DeleteAsync(string id, CancellationToken ct);
    Task<RunDetailDto?> SignupAsync(string runId, SignupRequest request, CancellationToken ct);
    Task<CharactersFetchResult> GetSignupOptionsAsync(string runId, CancellationToken ct);
    Task<RunDetailDto?> CancelSignupAsync(string runId, CancellationToken ct);
}
