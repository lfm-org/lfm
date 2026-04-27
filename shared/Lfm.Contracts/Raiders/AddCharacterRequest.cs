// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Raiders;

public sealed record AddCharacterRequest(string? Region, string? Realm, string? Name);
