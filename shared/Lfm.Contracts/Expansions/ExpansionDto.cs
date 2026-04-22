// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Contracts.Expansions;

/// <summary>
/// One row in the journal-expansion manifest. Carries the Blizzard expansion
/// id and display name only — everything else (icon, minimum level, raid /
/// dungeon membership) lives on the individual journal-instance rows via
/// <c>InstanceDto.ExpansionId</c> and is joined client-side.
///
/// Both properties are planned consumers of the create-run expansion selector
/// that ships in a follow-up PR. Tracked under the wire-payload-contract
/// "planned near-term feature reservation" exception — see
/// <c>docs/wire-payload-contract.md</c>. If the expansion selector is dropped,
/// remove this DTO (and the <c>/api/wow/reference/expansions</c> endpoint that
/// produces it) at the next audit.
/// </summary>
public sealed record ExpansionDto(int Id, string Name);
