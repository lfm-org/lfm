// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Characters;

namespace Lfm.Contracts.Runs;

/// <summary>
/// Run-scoped character options for the signup form, filtered to account-owned
/// characters that appear in the run guild roster.
/// </summary>
public sealed record RunSignupOptionsDto(IReadOnlyList<CharacterDto> Characters);
