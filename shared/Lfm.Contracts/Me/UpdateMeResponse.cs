// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Me;

/// <summary>
/// Response shape for PATCH /api/me.
/// Fields mirror the TypeScript handler at functions/src/functions/me-update.ts.
/// </summary>
public sealed record UpdateMeResponse(string Locale);
