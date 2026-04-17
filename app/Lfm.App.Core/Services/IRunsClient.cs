// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Runs;

namespace Lfm.App.Services;

public interface IRunsClient
{
    Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct);
    Task<RunDetailDto?> GetAsync(string id, CancellationToken ct);
    Task<RunDetailDto?> CreateAsync(CreateRunRequest request, CancellationToken ct);
    Task<RunDetailDto?> UpdateAsync(string id, UpdateRunRequest request, CancellationToken ct);
    Task<bool> DeleteAsync(string id, CancellationToken ct);
    Task<RunDetailDto?> SignupAsync(string runId, SignupRequest request, CancellationToken ct);
    Task<RunDetailDto?> CancelSignupAsync(string runId, CancellationToken ct);
}
