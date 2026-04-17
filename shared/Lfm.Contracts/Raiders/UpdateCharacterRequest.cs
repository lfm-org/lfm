// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Raiders;

/// <summary>
/// Response body for PUT /api/raider/characters/{id}.
/// Confirms the newly selected character ID.
/// </summary>
public sealed record UpdateCharacterResponse(string SelectedCharacterId);
