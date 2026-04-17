// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Contracts.Characters;

namespace Lfm.Contracts.Raiders;

public sealed record AddCharacterResponse(string SelectedCharacterId, CharacterDto Character);
