// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Characters;

/// <summary>
/// A WoW character belonging to the logged-in raider's Battle.net account.
/// Mirrors the <c>AccountCharacter</c> TypeScript interface in
/// <c>functions/src/types/index.ts</c> and the shape produced by
/// <c>toAccountCharacterViews</c> in <c>functions/src/lib/blizzard-adapters.ts</c>.
/// </summary>
public sealed record CharacterDto(
    string Name,
    string Realm,
    string RealmName,
    int Level,
    string Region,
    int? ClassId = null,
    string? ClassName = null,
    string? PortraitUrl = null,
    int? ActiveSpecId = null,
    string? SpecName = null);
