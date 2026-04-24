// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Lfm.Api.Services;
using Microsoft.Extensions.Logging;

namespace Lfm.Api.Audit;

/// <summary>
/// Writes audit events as structured logs. Actor identifiers are passed
/// through an <see cref="IActorHasher"/> before emission so the plaintext
/// battleNetId never reaches Application Insights.
///
/// <para>
/// The hasher is an ambient static dependency installed once from
/// <c>Program.cs</c> via <see cref="ConfigureHasher"/>. The default is an
/// <see cref="IdentityActorHasher"/> that returns the raw id — fine for
/// unit tests, which assert on the plaintext value. Production wiring
/// swaps to an <see cref="HmacActorHasher"/> keyed from
/// <c>AuditOptions.HashSalt</c>, so any deployed environment logs hex
/// digests instead of plaintext.
/// </para>
/// </summary>
public static class AuditLog
{
    private static IActorHasher _hasher = new IdentityActorHasher();

    /// <summary>
    /// Installs the hasher used for every subsequent <see cref="Emit"/>
    /// call. Called once at startup from <c>Program.cs</c>; never from
    /// application code paths.
    /// </summary>
    public static void ConfigureHasher(IActorHasher hasher)
    {
        _hasher = hasher;
    }

    public static void Emit(ILogger logger, AuditEvent evt)
    {
        var actor = _hasher.Hash(evt.ActorId);
        logger.LogInformation(
            "Audit: {AuditAction} actor={AuditActorId} target={AuditTargetId} result={AuditResult} detail={AuditDetail}",
            evt.Action,
            actor,
            evt.TargetId ?? "-",
            evt.Result,
            evt.Detail ?? "-");
    }
}
