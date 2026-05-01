// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Repositories;

namespace Lfm.Api.Runs;

public interface IRunSignupEligibility
{
    Task<bool> IsSignupCharacterInRunGuildAsync(
        RunDocument run,
        StoredSelectedCharacter signupCharacter,
        CancellationToken ct);
}
