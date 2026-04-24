// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using System.Security.Cryptography;
using System.Text;

namespace Lfm.Api.Services;

/// <summary>
/// Maps raw actor identifiers (battleNetIds) to an opaque, stable,
/// non-reversible hash suitable for telemetry. Instances are injected into
/// <see cref="Audit.AuditLog"/> at startup and used for the structured
/// <c>AuditActorId</c> property emitted to Application Insights.
/// </summary>
public interface IActorHasher
{
    /// <summary>
    /// Returns the hashed representation of <paramref name="actorId"/>.
    /// Empty or null input is returned verbatim as <c>"-"</c> so downstream
    /// log fields never carry an empty string.
    /// </summary>
    string Hash(string? actorId);
}

/// <summary>
/// No-op hasher for tests and local dev: returns the raw actor id. All
/// existing structural log assertions continue to pass because they expect
/// the plaintext battleNetId on <c>AuditActorId</c>.
/// </summary>
public sealed class IdentityActorHasher : IActorHasher
{
    public string Hash(string? actorId) =>
        string.IsNullOrEmpty(actorId) ? "-" : actorId;
}

/// <summary>
/// HMAC-SHA-256 hasher keyed by a configured salt. Produces a deterministic
/// lowercase hex string per actor, so repeated events for the same user
/// correlate in App Insights while the plaintext battleNetId never leaves
/// the application.
/// </summary>
public sealed class HmacActorHasher : IActorHasher, IDisposable
{
    private readonly HMACSHA256 _hmac;

    public HmacActorHasher(string salt)
    {
        if (string.IsNullOrEmpty(salt))
            throw new ArgumentException("Salt must not be empty.", nameof(salt));
        _hmac = new HMACSHA256(Encoding.UTF8.GetBytes(salt));
    }

    public string Hash(string? actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            return "-";
        var bytes = _hmac.ComputeHash(Encoding.UTF8.GetBytes(actorId));
        return Convert.ToHexStringLower(bytes);
    }

    public void Dispose() => _hmac.Dispose();
}
