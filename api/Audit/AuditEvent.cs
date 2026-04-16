// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

namespace Lfm.Api.Audit;

public sealed record AuditEvent(
    string Action,      // e.g. "login.success", "run.create", "account.delete"
    string ActorId,     // battleNetId (opaque numeric, not PII)
    string? TargetId,   // e.g. run ID, guild ID
    string Result,      // "success" or "failure"
    string? Detail);    // e.g. "missing login_state cookie", "permission_denied"
