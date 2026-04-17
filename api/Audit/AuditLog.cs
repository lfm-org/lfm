// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Microsoft.Extensions.Logging;

namespace Lfm.Api.Audit;

public static class AuditLog
{
    public static void Emit(ILogger logger, AuditEvent evt)
    {
        logger.LogInformation(
            "Audit: {AuditAction} actor={AuditActorId} target={AuditTargetId} result={AuditResult} detail={AuditDetail}",
            evt.Action,
            evt.ActorId,
            evt.TargetId ?? "-",
            evt.Result,
            evt.Detail ?? "-");
    }
}
