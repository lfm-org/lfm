// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Auth;
using Lfm.Contracts.Runs;

namespace Lfm.Api.Runs;

public abstract record RunSignupOptionsResult
{
    public sealed record Ok(RunSignupOptionsDto Options) : RunSignupOptionsResult;

    public sealed record NeedsRefresh : RunSignupOptionsResult;

    public sealed record NotFound(string Code, string Message) : RunSignupOptionsResult;

    public sealed record Forbidden(string Code, string Message) : RunSignupOptionsResult;
}

public interface IRunSignupOptionsService
{
    Task<RunSignupOptionsResult> GetAsync(string runId, SessionPrincipal principal, CancellationToken ct);
}
